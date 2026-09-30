# SOMNO SHOP – Belső szoftver

A szaküzlet belső folyamatainak modernizálása. Ez a repó a stratégiai vázlat
alapján épül fel, moduláris megközelítéssel.

## Megvalósított rész

**I. Modul / A) – Online GDPR és Adatlap-kitöltő Rendszer** (teljes)

- Távoli páciens-portál (e-mail/SMS linkes, token alapú kitöltés)
- Helyszíni (pulti/tabletes) kitöltés régi ügyfél diszkrét előhívásával
  (születési dátum alapján, névazonosság / „nincs találat” elágazással)
- Intelligens cím-adatbázis (irányítószám → település automatika)
- Jogi bizonyíték rögzítése véglegesítéskor (időbélyeg + IP), módosíthatatlan audit-napló
- A 4 kötelező hozzájárulási kérdés rögzítése
- KVL felé történő profil-visszaírás (jelenleg mock kliens mögött)
- **„Zéró hozzájárulás” és számla-parkoltatási radar**: ha nincs postai/e-mail
  hozzájárulás, a számla parkolva marad (1 hónap), pulti feladat generálódik, és az
  egyeztetés kimenetelei (téves kitöltés / személyes átvétel / időzített lezárás) kezeltek
- **Kihordási idő automatizmus**: lejáratkor automatikus értesítő (csak hozzájárulással)
- **„Félig kész” postai rendelések + adatpótlási protokoll**: hiányos kontakt esetén
  zárolás, adatpótló link, majd aktiválás; lezáráskor rendszerüzenet + számla
- **Ügyfél-idővonal (Timeline/History)** minden fő eseményre
- **Pulti áttekintő (Dashboard)**: nyitott feladatok, parkoltatott számlák, függő rendelések
- **Ütemezett automatizmusok** (háttérfolyamat): parkoltatás-lezárás, kihordási értesítők

**I. Modul / B) – Telefonközpont (Call Center) és Intelligens IVR**

- **Központi Nyitvatartás + Ünnepnapi Naptár modul** – a cég egyetlen igazságforrása:
  alap heti nyitvatartás + egyedi felülírások (ünnep, ledolgozós szombat, rövidített).
  Időzóna-helyes (Europe/Budapest) „nyitva van-e most?” döntés és következő nyitás.
- **Kifelé szinkron** (webshop „Kapcsolat” + Google Térkép) absztrakció mögött, mock
  implementációval; bármely naptár-módosítás automatikusan szinkronizál.
- **Bejövő hívás feldolgozás** (a telefonközpont hívja): nyitvatartási zsilip,
  CRM-találat telefonszám alapján (normalizálva), IVR menüpont-jelzés, adatlap „megnyitása”
  a Timeline-on, idős/legacy beteg jelzése.
- **Szelektív hangrögzítés-kezelés** (9-es gomb / „Hangrögzítés leállítása”) módosíthatatlan
  jogi **Audit Trail**-lel (időbélyeg, kezelő, ok).
- **Önürítő visszahívási lista**: nem fogadott / foglalt pult / munkaidőn kívüli igények;
  automatikus lezárás, ha a számot időközben elérték.
- **REST API a külső telefonközpontnak** (`/api/callcenter/...`): opening-status, incoming,
  answered, end, recording, callback.
- **Nyitvatartás admin UI** (`/nyitvatartas`).

## Architektúra

Réteges felépítés, hogy a KVL vállalatirányítási rendszer API-jaira később
fájdalommentesen rá lehessen kötni:

```
src/
  Szakuzlet.Domain          – entitások, enumok (KVL-független)
  Szakuzlet.Application     – szolgáltatások, DTO-k, IKvlClient absztrakció
  Szakuzlet.Infrastructure  – EF Core (PostgreSQL), KVL mock, cím-kereső
  Szakuzlet.Web             – Blazor Server (páciens-portál + pulti felület)
tests/
  Szakuzlet.Tests           – xUnit integrációs tesztek
```

### KVL integráció

A KVL fejlesztők jelenleg csak API végpontokat tudnak létrehozni. Amíg ezek nem
állnak rendelkezésre, a `MockKvlClient` szolgálja ki az `IKvlClient` interfészt.
A valós végpontok elkészültekor elég egy HTTP-alapú implementációt bekötni a
`DependencyInjection`-ben – az alkalmazás többi része változatlan marad.

## Technológia

- .NET 9 / ASP.NET Core / Blazor Server
- Entity Framework Core
- **Adatbázis:** PostgreSQL (éles, a céges szerveren).
  Fejlesztéshez SQLite is választható a `Database:Provider` beállítással,
  a Domain-modell módosítása nélkül.

## Futtatás

Éles (PostgreSQL) – az `appsettings.json` `ConnectionStrings:Default` beállítása szükséges:

```bash
dotnet run --project src/Szakuzlet.Web
```

Fejlesztés (SQLite, nincs szükség külön DB-re):

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Szakuzlet.Web
```

- Kezdőlap: `/`
- Pulti kiszolgálás: `/pult`
- Pulti áttekintő (Dashboard): `/dashboard`
- Nyitvatartás admin: `/nyitvatartas`
- Páciens-portál: `/adatlap/{token}` (a linket a kezdőlap „Demo link” gombja generálja)
- Rendelés-adatpótlás: `/rendeles-adatpotlas/{token}`

### Telefonközpont REST API (a külső VoIP hívja)

- `GET  /api/callcenter/opening-status` – nyitva van-e most (IVR zsilip)
- `POST /api/callcenter/incoming` – bejövő hívás (CRM-találat + menüjelzés)
- `POST /api/callcenter/{callId}/answered` – hívás fogadva (önüríti a visszahívást)
- `POST /api/callcenter/{callId}/end` – hívás vége (nem fogadott → visszahívási lista)
- `POST /api/callcenter/{callId}/recording` – hangrögzítés döntés + Audit Trail
- `POST /api/callcenter/callback/after-hours` – munkaidőn kívüli visszahívás
- `POST /api/callcenter/callback/busy-desk` – foglalt pult miatti visszahívás

## Tesztek

```bash
dotnet test
```

## Adatbázis-migrációk (PostgreSQL)

```bash
dotnet ef migrations add <Név> --project src/Szakuzlet.Infrastructure --startup-project src/Szakuzlet.Web -o Persistence/Migrations
```
