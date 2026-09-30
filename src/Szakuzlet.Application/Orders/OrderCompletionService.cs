using Microsoft.EntityFrameworkCore;
using Szakuzlet.Application.Abstractions;
using Szakuzlet.Application.Common;
using Szakuzlet.Application.Notifications;
using Szakuzlet.Domain.Entities;
using Szakuzlet.Domain.Enums;

namespace Szakuzlet.Application.Orders;

/// <summary>A hiányzó adatok pótlásához kiküldött link.</summary>
public record OrderCompletionLink(Guid OrderId, string Token);

/// <summary>
/// „Félig kész” postai rendelések és adatpótlási protokoll (I. Modul A, feladatlista 10-11. pont).
///
/// Ha a postai megrendelés rögzítésekor a kötelező kontaktadatok hiányosak, a rendszer letiltja
/// az azonnali lezárást/számlázást, „Adatpótlásra váró” státuszba teszi, és adatpótló linket küld.
/// Az adatok beküldése után a rendelés „Kiszolgálható” lesz. A lezáráskor rendszerüzenet + számla megy ki.
/// </summary>
public sealed class OrderCompletionService
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly EventRecorder _events;
    private readonly INotificationSender _notifications;
    private readonly ITokenGenerator _tokens;

    public OrderCompletionService(IAppDbContext db, IClock clock, EventRecorder events,
        INotificationSender notifications, ITokenGenerator tokens)
    {
        _db = db;
        _clock = clock;
        _events = events;
        _notifications = notifications;
        _tokens = tokens;
    }

    /// <summary>
    /// Postai rendelés rögzítése. Hiányos kontakt esetén „Adatpótlásra váró” státusz +
    /// adatpótló link kiküldése. Egyébként azonnal „Kiszolgálható”.
    /// </summary>
    public async Task<Order> RegisterAsync(Guid patientId, string number, CancellationToken ct = default)
    {
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == patientId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen páciens: {patientId}");

        var now = _clock.UtcNow;
        var order = new Order
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            Number = number,
            CreatedAtUtc = now
        };

        // A postázáshoz mobil VAGY e-mail kell (a "félig kész" definíciója szerint).
        var missingContact = string.IsNullOrWhiteSpace(patient.MobilePhone)
                             && string.IsNullOrWhiteSpace(patient.Email);

        if (missingContact)
        {
            order.Status = OrderStatus.AdatpotlasraVar;
            order.DataCompletionToken = _tokens.CreateToken();
            _db.Orders.Add(order);

            _events.Audit("Order", order.Id.ToString(), "RendelesFuggo_Adatpotlas",
                $"Rendelés: {number}; hiányzó kapcsolattartási adatok.");
            _events.Timeline(patientId, "PostaiRendelesFuggo",
                $"A(z) {number} postai rendelés adatpótlásra vár (hiányzó mobil/e-mail).");

            await SendDataCompletionRequestAsync(patient, order, ct);
        }
        else
        {
            order.Status = OrderStatus.Kiszolgalhato;
            _db.Orders.Add(order);
            _events.Audit("Order", order.Id.ToString(), "RendelesKiszolgalhato", $"Rendelés: {number}");
        }

        await _db.SaveChangesAsync(ct);
        return order;
    }

    /// <summary>Adatpótló rendelés betöltése token alapján (a linkre kattintva).</summary>
    public Task<Order?> GetByTokenAsync(string token, CancellationToken ct = default)
        => _db.Orders.Include(o => o.Patient)
            .FirstOrDefaultAsync(o => o.DataCompletionToken == token, ct);

    /// <summary>
    /// A páciens beküldi a hiányzó adatokat a linken keresztül. A profil frissül,
    /// a rendelés „Kiszolgálható” státuszba kerül (zöld lámpa a pultosnak).
    /// </summary>
    public async Task CompleteDataAsync(string token, string? mobile, string? email, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Patient)
            .FirstOrDefaultAsync(o => o.DataCompletionToken == token, ct)
            ?? throw new InvalidOperationException("Érvénytelen adatpótló token.");

        if (order.Status != OrderStatus.AdatpotlasraVar)
            throw new InvalidOperationException("Ez a rendelés már nem vár adatpótlásra.");

        var now = _clock.UtcNow;
        var patient = order.Patient;
        if (!string.IsNullOrWhiteSpace(mobile)) patient.MobilePhone = mobile.Trim();
        if (!string.IsNullOrWhiteSpace(email)) patient.Email = email.Trim();
        patient.UpdatedAtUtc = now;

        if (string.IsNullOrWhiteSpace(patient.MobilePhone) && string.IsNullOrWhiteSpace(patient.Email))
            throw new InvalidOperationException("Legalább egy elérhetőség (mobil vagy e-mail) megadása kötelező.");

        order.Status = OrderStatus.Kiszolgalhato;
        order.DataCompletionToken = null; // a link elhasználódott

        _events.Audit("Order", order.Id.ToString(), "AdatpotlasBekuldve", actor: "PACIENS");
        _events.Timeline(patient.Id, "PostaiRendelesKiszolgalhato",
            $"A(z) {order.Number} rendelés adatai pótolva, kiszolgálható.");

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// A csomag lezárása. Csak „Kiszolgálható” rendelést lehet lezárni. A lezáráskor
    /// automata rendszerüzenet + digitális számla megy ki, és jelezzük az adatlap frissítését.
    /// (I. Modul A, feladatlista 11. pont)
    /// </summary>
    public async Task CloseAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await _db.Orders.Include(o => o.Patient)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new InvalidOperationException($"Nincs ilyen rendelés: {orderId}");

        if (order.Status == OrderStatus.AdatpotlasraVar)
            throw new InvalidOperationException("Adatpótlásra váró rendelés nem zárható le.");
        if (order.Status == OrderStatus.Lezarva)
            throw new InvalidOperationException("A rendelés már lezárva.");

        var now = _clock.UtcNow;
        order.Status = OrderStatus.Lezarva;
        order.ClosedAtUtc = now;

        _events.Audit("Order", order.Id.ToString(), "RendelesLezarva", $"Rendelés: {order.Number}");
        _events.Timeline(order.PatientId, "RendelesLezarva",
            $"A(z) {order.Number} postai rendelés lezárva, számla kiküldve.");

        var patient = order.Patient;
        if (!string.IsNullOrWhiteSpace(patient.Email))
        {
            await _notifications.SendAsync(new NotificationMessage(
                NotificationKind.Email, patient.Email!,
                "Köszönjük megrendelését!",
                "Köszönjük megrendelését! Mellékeljük a digitális számlát. " +
                "Tájékoztatjuk, hogy a megrendelés során megadott adataival a központi " +
                "adatlapját automatikusan frissítettük, amelyet a hivatkozott linken tud ellenőrizni."), ct);
        }
        else if (!string.IsNullOrWhiteSpace(patient.MobilePhone))
        {
            await _notifications.SendAsync(new NotificationMessage(
                NotificationKind.Sms, patient.MobilePhone!,
                "Rendelés lezárva",
                "Köszönjük megrendelését! A számláját elkészítettük."), ct);
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SendDataCompletionRequestAsync(Patient patient, Order order, CancellationToken ct)
    {
        var text = "Megrendelését megkaptuk! A csomag feladásához és a számla kiállításához " +
                   "kérjük, kattintson a linkre, és 1 perc alatt töltse ki a hiányzó adatokat.";
        // Amelyik csatorna elérhető, azon értesítünk (hiányos ág: gyakran egyik sincs, ekkor a pult intézi).
        if (!string.IsNullOrWhiteSpace(patient.Email))
            await _notifications.SendAsync(new NotificationMessage(
                NotificationKind.Email, patient.Email!, "Hiányzó adatok pótlása", text), ct);
        else if (!string.IsNullOrWhiteSpace(patient.MobilePhone))
            await _notifications.SendAsync(new NotificationMessage(
                NotificationKind.Sms, patient.MobilePhone!, "Hiányzó adatok pótlása", text), ct);
    }
}
