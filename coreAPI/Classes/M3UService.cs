using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using coreAPI.Models;

namespace coreAPI.Classes
{
    public class M3UService
    {
        private readonly string _filePath;
        private readonly string _databasePath;
        private readonly int _backupRetention;
        private static readonly object _fileLock = new();

        private static readonly Regex GroupRegex = new(@"group-title=""(.*?)""", RegexOptions.Compiled);
        private static readonly Regex LogoRegex = new(@"tvg-logo=""(.*?)""", RegexOptions.Compiled);
        private static readonly Regex TvgIdRegex = new(@"tvg-id=""(.*?)""", RegexOptions.Compiled);

        public M3UService(IOptions<M3UOptions> options)
        {
            _filePath = Path.GetFullPath(options.Value.FilePath);
            _databasePath = Path.GetFullPath(options.Value.DatabasePath);
            _backupRetention = Math.Max(0, options.Value.BackupRetention);

            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);

            if (!File.Exists(_filePath))
                File.WriteAllText(_filePath, "#EXTM3U\n", Encoding.UTF8);

            InitializeDatabase();
        }

        public List<M3UEntry> LoadEntries()
        {
            lock (_fileLock)
            {
                using var connection = OpenConnection();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT id, group_title, tvg_logo, channel_name, stream_url, tvg_id, sort_order
                    FROM channels
                    ORDER BY sort_order, id;
                    """;

                using var reader = command.ExecuteReader();
                var entries = new List<M3UEntry>();
                while (reader.Read())
                {
                    entries.Add(new M3UEntry
                    {
                        Id = reader.GetInt32(0),
                        GroupTitle = reader.GetString(1),
                        TVGLogo = reader.IsDBNull(2) ? null : reader.GetString(2),
                        ChannelName = reader.GetString(3),
                        StreamUrl = reader.GetString(4),
                        TVGId = reader.IsDBNull(5) ? null : reader.GetString(5),
                        Order = reader.GetInt32(6)
                    });
                }

                return entries;
            }
        }

        public List<M3UEntry> ParseEntries(string content)
        {
            var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            var entries = new List<M3UEntry>();
            var order = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (i + 1 >= lines.Length)
                    break;

                var url = lines[++i].Trim();
                var group = GroupRegex.Match(line);
                var logo = LogoRegex.Match(line);
                var tvgId = TvgIdRegex.Match(line);
                var namePartIndex = line.IndexOf(',', StringComparison.Ordinal);

                entries.Add(new M3UEntry
                {
                    GroupTitle = group.Success ? group.Groups[1].Value : "",
                    TVGLogo = logo.Success ? logo.Groups[1].Value : null,
                    TVGId = tvgId.Success ? tvgId.Groups[1].Value : null,
                    ChannelName = namePartIndex >= 0 ? line[(namePartIndex + 1)..].Trim() : "Unknown",
                    StreamUrl = url,
                    Order = order++
                });
            }

            return entries;
        }

        public void SaveEntries(List<M3UEntry> entries)
        {
            lock (_fileLock)
            {
                var ordered = entries.OrderBy(e => e.Order).ToList();
                for (var i = 0; i < ordered.Count; i++)
                    ordered[i].Order = i;

                using var connection = OpenConnection();
                using var transaction = connection.BeginTransaction();

                using (var delete = connection.CreateCommand())
                {
                    delete.Transaction = transaction;
                    delete.CommandText = "DELETE FROM channels;";
                    delete.ExecuteNonQuery();
                }

                foreach (var entry in ordered)
                    InsertEntry(connection, transaction, entry);

                transaction.Commit();

                CreateBackup();
                WriteM3uFile(ordered);
            }
        }

        public void MoveUp(int id)
        {
            var entries = LoadEntries();
            var index = entries.FindIndex(x => x.Id == id);
            if (index > 0)
            {
                (entries[index - 1].Order, entries[index].Order) =
                    (entries[index].Order, entries[index - 1].Order);
                SaveEntries(entries);
            }
        }

        public void MoveDown(int id)
        {
            var entries = LoadEntries();
            var index = entries.FindIndex(x => x.Id == id);
            if (index >= 0 && index < entries.Count - 1)
            {
                (entries[index + 1].Order, entries[index].Order) =
                    (entries[index].Order, entries[index + 1].Order);
                SaveEntries(entries);
            }
        }

        public void Reorder(IReadOnlyList<int> orderedIds)
        {
            var entries = LoadEntries();
            var entriesById = entries.ToDictionary(e => e.Id);
            var positions = entries
                .Select((entry, index) => new { entry.Id, index })
                .Where(x => orderedIds.Contains(x.Id))
                .Select(x => x.index)
                .ToList();

            var reorderedEntries = orderedIds
                .Where(entriesById.ContainsKey)
                .Select(id => entriesById[id])
                .ToList();

            for (var i = 0; i < positions.Count && i < reorderedEntries.Count; i++)
                entries[positions[i]] = reorderedEntries[i];

            for (var i = 0; i < entries.Count; i++)
                entries[i].Order = i;

            SaveEntries(entries);
        }

        private void InitializeDatabase()
        {
            using var connection = OpenConnection();
            using (var schema = connection.CreateCommand())
            {
                schema.CommandText = """
                    CREATE TABLE IF NOT EXISTS channels (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        group_title TEXT NOT NULL,
                        tvg_logo TEXT NULL,
                        channel_name TEXT NOT NULL,
                        stream_url TEXT NOT NULL,
                        tvg_id TEXT NULL,
                        sort_order INTEGER NOT NULL
                    );
                    """;
                schema.ExecuteNonQuery();
            }

            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM channels;";
            var channelCount = Convert.ToInt32(count.ExecuteScalar());
            if (channelCount > 0)
                return;

            var importedEntries = ParseEntries(File.ReadAllText(_filePath, Encoding.UTF8));
            if (importedEntries.Count == 0)
                return;

            using var transaction = connection.BeginTransaction();
            foreach (var entry in importedEntries)
                InsertEntry(connection, transaction, entry);
            transaction.Commit();
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            return connection;
        }

        private static void InsertEntry(SqliteConnection connection, SqliteTransaction transaction, M3UEntry entry)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = entry.Id > 0
                ? """
                  INSERT INTO channels (id, group_title, tvg_logo, channel_name, stream_url, tvg_id, sort_order)
                  VALUES ($id, $group_title, $tvg_logo, $channel_name, $stream_url, $tvg_id, $sort_order);
                  """
                : """
                  INSERT INTO channels (group_title, tvg_logo, channel_name, stream_url, tvg_id, sort_order)
                  VALUES ($group_title, $tvg_logo, $channel_name, $stream_url, $tvg_id, $sort_order);
                  SELECT last_insert_rowid();
                  """;

            command.Parameters.AddWithValue("$group_title", entry.GroupTitle ?? "");
            command.Parameters.AddWithValue("$tvg_logo", (object?)entry.TVGLogo ?? DBNull.Value);
            command.Parameters.AddWithValue("$channel_name", entry.ChannelName ?? "Unknown");
            command.Parameters.AddWithValue("$stream_url", entry.StreamUrl ?? "");
            command.Parameters.AddWithValue("$tvg_id", (object?)entry.TVGId ?? DBNull.Value);
            command.Parameters.AddWithValue("$sort_order", entry.Order);

            if (entry.Id > 0)
            {
                command.Parameters.AddWithValue("$id", entry.Id);
                command.ExecuteNonQuery();
            }
            else
            {
                entry.Id = Convert.ToInt32(command.ExecuteScalar());
            }
        }

        private void WriteM3uFile(List<M3UEntry> ordered)
        {
            var builder = new StringBuilder();
            builder.AppendLine("#EXTM3U");
            foreach (var entry in ordered)
            {
                var logo = string.IsNullOrWhiteSpace(entry.TVGLogo) ? "" : $" tvg-logo=\"{entry.TVGLogo}\"";
                var tvgId = string.IsNullOrWhiteSpace(entry.TVGId)
                    ? $" tvg-id=\"{entry.GroupTitle}\""
                    : $" tvg-id=\"{entry.TVGId}\"";
                var group = string.IsNullOrWhiteSpace(entry.GroupTitle) ? "Otros" : entry.GroupTitle;

                builder.AppendLine($"#EXTINF:-1{logo}{tvgId} group-title=\"{group}\", {entry.ChannelName}");
                builder.AppendLine(entry.StreamUrl);
            }

            var temporaryPath = _filePath + ".tmp";
            File.WriteAllText(temporaryPath, builder.ToString(), Encoding.UTF8);
            File.Copy(temporaryPath, _filePath, overwrite: true);
            File.Delete(temporaryPath);
        }

        private void CreateBackup()
        {
            if (!File.Exists(_filePath)) return;
            var directory = Path.GetDirectoryName(_filePath)!;
            var name = Path.GetFileNameWithoutExtension(_filePath);
            var extension = Path.GetExtension(_filePath);
            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backup = Path.Combine(directory, $"{name}.{stamp}{extension}.bak");
            File.Copy(_filePath, backup, overwrite: true);

            if (_backupRetention > 0)
            {
                var backups = Directory.GetFiles(directory, $"{name}.*{extension}.bak")
                    .OrderByDescending(f => f)
                    .ToList();
                foreach (var old in backups.Skip(_backupRetention))
                    File.Delete(old);
            }
        }
    }
}
