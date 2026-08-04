using System.Data;
using Dapper;

namespace Cerebro.Server.Data;

// SQLite n'a pas de type natif pour DateTimeOffset : on le stocke en TEXT (format "O", round-trip ISO 8601).
internal sealed class DateTimeOffsetTypeHandler : SqlMapper.TypeHandler<DateTimeOffset>
{
    public override void SetValue(IDbDataParameter parameter, DateTimeOffset value)
    {
        parameter.Value = value.ToString("O");
    }

    public override DateTimeOffset Parse(object value) => DateTimeOffset.Parse((string)value);
}
