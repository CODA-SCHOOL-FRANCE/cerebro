using Microsoft.Data.Sqlite;

namespace Cerebro.Server.Data;

// SQLite ouvre un fichier existant mais ne crée jamais le dossier parent manquant : sur un premier
// lancement (ex: connexion par défaut "db/cerebro.db" à côté de wwwroot), sans ça l'ouverture échoue
// avec "unable to open database file" plutôt que de simplement créer le fichier.
internal static class SqliteDatabaseFile
{
    public static void EnsureDirectoryExists(string connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
