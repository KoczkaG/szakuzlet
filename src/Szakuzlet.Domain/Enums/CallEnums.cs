namespace Szakuzlet.Domain.Enums;

/// <summary>A hívás iránya.</summary>
public enum CallDirection
{
    Bejovo = 0,
    Kimeno = 1
}

/// <summary>A hangrögzítés állapota (szelektív, GDPR-elágazással).</summary>
public enum RecordingState
{
    /// <summary>Rögzítés folyamatban / rögzítve (a beteg hozzájárult / nem tiltotta).</summary>
    Rogzitve = 0,

    /// <summary>A beteg megtagadta (9-es gomb) – nem indul rögzítés.</summary>
    Megtagadva = 1,

    /// <summary>A rögzítés elindult, majd a kezelő leállította és a sávot törölte.</summary>
    LeallitvaEsTorolve = 2
}

/// <summary>Egy visszahívási igény állapota.</summary>
public enum CallbackStatus
{
    /// <summary>Nyitott: vissza kell hívni.</summary>
    Nyitott = 0,

    /// <summary>Teljesítve: sikeresen visszahívtuk vagy időközben elértük.</summary>
    Teljesitve = 1
}

/// <summary>A visszahívási igény forrása / típusa.</summary>
public enum CallbackKind
{
    /// <summary>Munkaidőn kívüli visszahívás (9 órás zsilip a következő munkanapra).</summary>
    MunkaidonKivul = 0,

    /// <summary>Foglalt pult / túlterheltség miatti azonnali visszahívás.</summary>
    FoglaltPult = 1,

    /// <summary>Nem fogadott hívás (önürítő lista).</summary>
    NemFogadott = 2
}
