using Microsoft.EntityFrameworkCore;
using Szakuzlet.Infrastructure;
using Szakuzlet.Infrastructure.Persistence;
using Szakuzlet.Web.Api;
using Szakuzlet.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Belső szoftver rétegei (EF Core + PostgreSQL, KVL mock, szolgáltatások).
builder.Services.AddInfrastructure(builder.Configuration);

// Ütemezett automatizmusok (parkoltatás-lezárás, kihordási idő értesítők).
builder.Services.AddHostedService<Szakuzlet.Web.BackgroundJobs.AutomationWorker>();

var app = builder.Build();

// Séma előállítása indításkor. PostgreSQL alatt verziózott migrációk (éles),
// SQLite fejlesztői módban gyors EnsureCreated.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsNpgsql())
        db.Database.Migrate();
    else
        db.Database.EnsureCreated();

    await Szakuzlet.Infrastructure.Persistence.DataSeeder.SeedAsync(db);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// A telefonközpont (külső VoIP) REST végpontjai.
app.MapCallCenterApi();

app.Run();
