using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Szakuzlet.Infrastructure.Persistence;

/// <summary>
/// DateTimeOffset &lt;-&gt; long (Unix ms, UTC) konverzió. SQLite fejlesztői módban
/// biztosítja a rendezhetőséget/összehasonlíthatóságot. Az érték mindig UTC-ben kerül tárolásra.
/// </summary>
public sealed class DateTimeOffsetToLongConverter : ValueConverter<DateTimeOffset, long>
{
    public DateTimeOffsetToLongConverter()
        : base(
            v => v.ToUniversalTime().ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v))
    {
    }
}

public sealed class NullableDateTimeOffsetToLongConverter : ValueConverter<DateTimeOffset?, long?>
{
    public NullableDateTimeOffsetToLongConverter()
        : base(
            v => v.HasValue ? v.Value.ToUniversalTime().ToUnixTimeMilliseconds() : (long?)null,
            v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : (DateTimeOffset?)null)
    {
    }
}
