using Microsoft.Data.Sqlite;

namespace ModelGenerator.Data.Database;

public class ConnectionFactory
{
    private readonly string _connectionString;

    public ConnectionFactory(string databasePath)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }
}
