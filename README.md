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
- Páciens-portál: `/adatlap/{token}` (a linket a kezdőlap „Demo link” gombja generálja)
- Rendelés-adatpótlás: `/rendeles-adatpotlas/{token}`

## Tesztek

```bash
dotnet test
```

## Adatbázis-migrációk (PostgreSQL)

```bash
dotnet ef migrations add <Név> --project src/Szakuzlet.Infrastructure --startup-project src/Szakuzlet.Web -o Persistence/Migrations
```
