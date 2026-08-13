using static Dapper.SqlMapper;

namespace Cerebro.Server.Data;

// Enregistrement explicite plutôt qu'un constructeur statique attaché à un seul repository :
// avec plusieurs repositories Dapper (SqliteExamRepository, SqliteDashboardCredentialsStore), l'ordre
// de résolution DI ne garantit pas qu'un type précis soit touché en premier. Chaque constructeur
// appelle RegisterOnce() explicitement ; idempotent, donc sans risque à appeler plusieurs fois.
internal static class DapperTypeHandlers
{
    private static bool _registered;

    public static void RegisterOnce()
    {
        if (_registered)
        {
            return;
        }

        var handler = new DateTimeOffsetTypeHandler();
        AddTypeHandler(typeof(DateTimeOffset), handler);
        AddTypeHandler(typeof(DateTimeOffset?), handler);
        _registered = true;
    }
}