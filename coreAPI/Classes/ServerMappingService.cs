using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using coreAPI.Models;

namespace coreAPI.Classes
{
    public class ServerMappingService
    {
        private readonly string _databasePath;
        private readonly string _legacyJsonPath;
        private static readonly object _lock = new();

        public ServerMappingService(IOptions<M3UOptions> options)
        {
            _databasePath = Path.GetFullPath(options.Value.DatabasePath);
            _legacyJsonPath = Path.Combine(Path.GetDirectoryName(_databasePath)!, "userid.json");

            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
            InitializeDatabase();
        }

        public List<ServerMapping> LoadAll()
        {
            lock (_lock)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT id, name, base_url
                    FROM server_mappings
                    ORDER BY name COLLATE NOCASE, id;
                    """;

                using var reader = command.ExecuteReader();
                var mappings = new List<ServerMapping>();
                while (reader.Read())
                {
                    mappings.Add(new ServerMapping
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        BaseUrl = reader.GetString(2)
                    });
                }

                return mappings;
            }
        }

        public Dictionary<string, string> LoadDictionary()
        {
            return LoadAll()
                .ToDictionary(mapping => mapping.Name, mapping => mapping.BaseUrl, StringComparer.OrdinalIgnoreCase);
        }

        public ServerMapping? GetById(int id)
        {
            return LoadAll().FirstOrDefault(mapping => mapping.Id == id);
        }

        public void Create(ServerMapping mapping)
        {
            lock (_lock)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO server_mappings (name, base_url)
                    VALUES ($name, $base_url);
                    SELECT last_insert_rowid();
                    """;
                AddParameters(command, mapping);

                try
                {
                    mapping.Id = Convert.ToInt32(command.ExecuteScalar());
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
                {
                    throw new InvalidOperationException("Ya existe un servidor con ese identificador.", ex);
                }
            }
        }

        public bool Update(ServerMapping mapping)
        {
            lock (_lock)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    UPDATE server_mappings
                    SET name = $name, base_url = $base_url
                    WHERE id = $id;
                    """;
                command.Parameters.AddWithValue("$id", mapping.Id);
                AddParameters(command, mapping);

                try
                {
                    return command.ExecuteNonQuery() > 0;
                }
                catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
                {
                    throw new InvalidOperationException("Ya existe otro servidor con ese identificador.", ex);
                }
            }
        }

        public bool Delete(int id)
        {
            lock (_lock)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM server_mappings WHERE id = $id;";
                command.Parameters.AddWithValue("$id", id);
                return command.ExecuteNonQuery() > 0;
            }
        }

        private void InitializeDatabase()
        {
            using var connection = OpenConnection();
            using (var schema = connection.CreateCommand())
            {
                schema.CommandText = """
                    CREATE TABLE IF NOT EXISTS server_mappings (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        name TEXT NOT NULL COLLATE NOCASE UNIQUE,
                        base_url TEXT NOT NULL
                    );
                    """;
                schema.ExecuteNonQuery();
            }

            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM server_mappings;";
            if (Convert.ToInt32(count.ExecuteScalar()) > 0 || !File.Exists(_legacyJsonPath))
                return;

            Dictionary<string, string>? legacyMappings;
            try
            {
                var json = File.ReadAllText(_legacyJsonPath, Encoding.UTF8);
                legacyMappings = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            }
            catch (JsonException)
            {
                return;
            }

            if (legacyMappings == null || legacyMappings.Count == 0)
                return;

            using var transaction = connection.BeginTransaction();
            foreach (var mapping in legacyMappings)
            {
                using var insert = connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO server_mappings (name, base_url) VALUES ($name, $base_url);";
                insert.Parameters.AddWithValue("$name", mapping.Key);
                insert.Parameters.AddWithValue("$base_url", mapping.Value);
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            return connection;
        }

        private static void AddParameters(SqliteCommand command, ServerMapping mapping)
        {
            command.Parameters.AddWithValue("$name", mapping.Name.Trim());
            command.Parameters.AddWithValue("$base_url", mapping.BaseUrl.Trim().TrimEnd('/'));
        }
    }
}
