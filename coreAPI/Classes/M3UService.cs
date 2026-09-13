using System.Text;
using System.Globalization;
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
        private static readonly Regex AceStreamIdRegex = new(@"(?:^|[?&])id=([^&#]+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TrailingAsterisksRegex = new(@"\*{1,2}\s*$", RegexOptions.Compiled);
        private static readonly NaturalStringComparer EntryNameComparer = new();
        public const string DefaultSyncSourceUrl = "https://git.gay/TokyoGhoulles/AceStream_IDs/raw/branch/main/hashes.m3u";

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
                    SELECT id, group_title, tvg_logo, channel_name, stream_url, tvg_id
                    FROM channels
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
                        TVGId = reader.IsDBNull(5) ? null : reader.GetString(5)
                    });
                }

                return OrderEntries(entries);
            }
        }

        public List<M3UEntry> ParseEntries(string content)
        {
            var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);
            var entries = new List<M3UEntry>();

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
                    StreamUrl = url
                });
            }

            return entries;
        }

        public void SaveEntries(List<M3UEntry> entries)
        {
            SaveEntries(entries, null);
        }

        private void SaveEntries(List<M3UEntry> entries, string? header)
        {
            lock (_fileLock)
            {
                var ordered = OrderEntries(entries);

                using var connection = OpenConnection();
                var storedHeader = header ?? LoadPlaylistHeader(connection);
                using var transaction = connection.BeginTransaction();

                using (var delete = connection.CreateCommand())
                {
                    delete.Transaction = transaction;
                    delete.CommandText = "DELETE FROM channels;";
                    delete.ExecuteNonQuery();
                }

                foreach (var entry in ordered)
                    InsertEntry(connection, transaction, entry);

                if (header is not null)
                    SavePlaylistHeader(connection, transaction, header);

                transaction.Commit();

                CreateBackup();
                WriteM3uFile(ordered, storedHeader);
            }
        }

        public async Task<SyncResult> SynchronizeAsync(HttpClient client, CancellationToken cancellationToken = default)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, DefaultSyncSourceUrl);
            request.Headers.UserAgent.ParseAdd("coreAPI-M3U-Synchronizer/1.0");
            request.Headers.Accept.ParseAdd("audio/x-mpegurl");

            using var response = await client.SendAsync(request, cancellationToken);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var remoteEntries = ParseEntries(content);
            var remoteById = remoteEntries
                .Select(entry => (Entry: entry, Id: ExtractAceStreamId(entry.StreamUrl)))
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id!, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToDictionary(item => item.Id!, item => item.Entry, StringComparer.OrdinalIgnoreCase);

            if (remoteById.Count == 0)
                throw new InvalidOperationException("La fuente remota no contiene canales AceStream válidos.");

            var localEntries = LoadEntries();
            var localById = localEntries
                .Select(entry => (Entry: entry, Id: ExtractAceStreamId(entry.StreamUrl)))
                .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                .GroupBy(item => item.Id!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Entry, StringComparer.OrdinalIgnoreCase);
            var localEntriesWithId = localEntries.Count(entry => !string.IsNullOrWhiteSpace(ExtractAceStreamId(entry.StreamUrl)));

            var merged = new List<M3UEntry>();
            var updated = 0;
            var added = 0;

            foreach (var (id, remoteEntry) in remoteById)
            {
                remoteEntry.StreamUrl = $"acestream://{id}";
                remoteEntry.ChannelName = TrailingAsterisksRegex
                    .Replace(remoteEntry.ChannelName ?? "", "")
                    .Trim();
                remoteEntry.ChannelName = ToProperCase(remoteEntry.ChannelName);
                remoteEntry.GroupTitle = ToProperCase(remoteEntry.GroupTitle);

                if (localById.TryGetValue(id, out var localEntry))
                {
                    remoteEntry.Id = localEntry.Id;
                    updated++;
                }
                else
                {
                    remoteEntry.Id = 0;
                    added++;
                }

                merged.Add(remoteEntry);
            }

            var preserved = 0;
            foreach (var localEntry in localEntries)
            {
                var localId = ExtractAceStreamId(localEntry.StreamUrl);
                if (!string.IsNullOrWhiteSpace(localId))
                {
                    if (remoteById.ContainsKey(localId))
                        continue;

                    if (localById[localId] != localEntry)
                        continue;
                }

                merged.Add(localEntry);
                preserved++;
            }

            var duplicatesRemoved = (remoteEntries.Count - remoteById.Count) +
                                    (localEntriesWithId - localById.Count);
            SaveEntries(merged, ExtractPlaylistHeader(content));

            return new SyncResult(added, updated, preserved, Math.Max(0, duplicatesRemoved));
        }

        private static string? ExtractAceStreamId(string? streamUrl)
        {
            if (string.IsNullOrWhiteSpace(streamUrl))
                return null;

            var value = streamUrl.Trim();
            const string aceStreamPrefix = "acestream://";
            if (value.StartsWith(aceStreamPrefix, StringComparison.OrdinalIgnoreCase))
                return value[aceStreamPrefix.Length..].Trim();

            var match = AceStreamIdRegex.Match(value);
            return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value).Trim() : null;
        }

        private static string ToProperCase(string? value)
        {
            var normalized = Regex.Replace(value?.Trim() ?? "", @"\s+", " ");
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(normalized.ToLower(CultureInfo.CurrentCulture));
        }

        private static string ExtractPlaylistHeader(string content)
        {
            var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            var firstEntry = Array.FindIndex(lines, line =>
                line.TrimStart().StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase));

            if (firstEntry < 0)
                throw new InvalidOperationException("La fuente remota no contiene una cabecera M3U válida.");

            var header = string.Join("\n", lines.Take(firstEntry)).TrimEnd();
            if (!header.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La fuente remota no comienza con #EXTM3U.");

            return header;
        }

        private static List<M3UEntry> OrderEntries(IEnumerable<M3UEntry> entries)
        {
            return entries
                .OrderBy(e => e.GroupTitle ?? "", EntryNameComparer)
                .ThenBy(e => e.ChannelName ?? "", EntryNameComparer)
                .ThenBy(e => e.Id)
                .ToList();
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
                        tvg_id TEXT NULL
                    );
                    """;
                schema.ExecuteNonQuery();
            }

            using (var metadata = connection.CreateCommand())
            {
                metadata.CommandText = """
                    CREATE TABLE IF NOT EXISTS playlist_metadata (
                        id INTEGER PRIMARY KEY CHECK (id = 1),
                        header TEXT NOT NULL
                    );
                    INSERT INTO playlist_metadata (id, header)
                    SELECT 1, '#EXTM3U'
                    WHERE NOT EXISTS (SELECT 1 FROM playlist_metadata WHERE id = 1);
                    """;
                metadata.ExecuteNonQuery();
            }

            MigrateSortOrderColumn(connection);

            using var count = connection.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM channels;";
            var channelCount = Convert.ToInt32(count.ExecuteScalar());
            if (channelCount > 0)
                return;

            var importedEntries = ParseEntries(File.ReadAllText(_filePath, Encoding.UTF8));
            if (importedEntries.Count == 0)
                return;

            importedEntries = OrderEntries(importedEntries);

            using var transaction = connection.BeginTransaction();
            foreach (var entry in importedEntries)
                InsertEntry(connection, transaction, entry);
            transaction.Commit();
        }

        private static void MigrateSortOrderColumn(SqliteConnection connection)
        {
            using var columns = connection.CreateCommand();
            columns.CommandText = "PRAGMA table_info(channels);";
            using var reader = columns.ExecuteReader();
            var hasSortOrder = false;
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), "sort_order", StringComparison.OrdinalIgnoreCase))
                {
                    hasSortOrder = true;
                    break;
                }
            }
            reader.Dispose();

            if (!hasSortOrder)
                return;

            using var transaction = connection.BeginTransaction();
            using var migrate = connection.CreateCommand();
            migrate.Transaction = transaction;
            migrate.CommandText = """
                CREATE TABLE channels_new (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    group_title TEXT NOT NULL,
                    tvg_logo TEXT NULL,
                    channel_name TEXT NOT NULL,
                    stream_url TEXT NOT NULL,
                    tvg_id TEXT NULL
                );
                INSERT INTO channels_new (id, group_title, tvg_logo, channel_name, stream_url, tvg_id)
                    SELECT id, group_title, tvg_logo, channel_name, stream_url, tvg_id FROM channels;
                DROP TABLE channels;
                ALTER TABLE channels_new RENAME TO channels;
                """;
            migrate.ExecuteNonQuery();
            transaction.Commit();
        }

        private SqliteConnection OpenConnection()
        {
            var connection = new SqliteConnection($"Data Source={_databasePath}");
            connection.Open();
            return connection;
        }

        private static string LoadPlaylistHeader(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT header FROM playlist_metadata WHERE id = 1;";
            return command.ExecuteScalar() as string ?? "#EXTM3U";
        }

        private static void SavePlaylistHeader(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string header)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO playlist_metadata (id, header)
                VALUES (1, $header)
                ON CONFLICT(id) DO UPDATE SET header = excluded.header;
                """;
            command.Parameters.AddWithValue("$header", header);
            command.ExecuteNonQuery();
        }

        private static void InsertEntry(SqliteConnection connection, SqliteTransaction transaction, M3UEntry entry)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = entry.Id > 0
                ? """
                  INSERT INTO channels (id, group_title, tvg_logo, channel_name, stream_url, tvg_id)
                  VALUES ($id, $group_title, $tvg_logo, $channel_name, $stream_url, $tvg_id);
                  """
                : """
                  INSERT INTO channels (group_title, tvg_logo, channel_name, stream_url, tvg_id)
                  VALUES ($group_title, $tvg_logo, $channel_name, $stream_url, $tvg_id);
                  SELECT last_insert_rowid();
                  """;

            command.Parameters.AddWithValue("$group_title", entry.GroupTitle ?? "");
            command.Parameters.AddWithValue("$tvg_logo", (object?)entry.TVGLogo ?? DBNull.Value);
            command.Parameters.AddWithValue("$channel_name", entry.ChannelName ?? "Unknown");
            command.Parameters.AddWithValue("$stream_url", entry.StreamUrl ?? "");
            command.Parameters.AddWithValue("$tvg_id", (object?)entry.TVGId ?? DBNull.Value);
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

        private void WriteM3uFile(List<M3UEntry> ordered, string header)
        {
            var builder = new StringBuilder();
            builder.AppendLine(header.TrimEnd('\r', '\n'));
            builder.AppendLine();
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

        private sealed class NaturalStringComparer : IComparer<string>
        {
            private static readonly Regex PartsRegex = new(@"(\d+)", RegexOptions.Compiled);
            private readonly CompareInfo _compareInfo = CultureInfo.CurrentCulture.CompareInfo;

            public int Compare(string? left, string? right)
            {
                if (ReferenceEquals(left, right)) return 0;
                if (left is null) return -1;
                if (right is null) return 1;

                var leftParts = PartsRegex.Split(left.Trim());
                var rightParts = PartsRegex.Split(right.Trim());
                var count = Math.Min(leftParts.Length, rightParts.Length);

                for (var i = 0; i < count; i++)
                {
                    var leftPart = leftParts[i];
                    var rightPart = rightParts[i];
                    var leftIsNumber = long.TryParse(leftPart, out _);
                    var rightIsNumber = long.TryParse(rightPart, out _);

                    int comparison;
                    if (leftIsNumber && rightIsNumber)
                    {
                        var leftNumber = leftPart.TrimStart('0');
                        var rightNumber = rightPart.TrimStart('0');
                        leftNumber = leftNumber.Length == 0 ? "0" : leftNumber;
                        rightNumber = rightNumber.Length == 0 ? "0" : rightNumber;

                        comparison = leftNumber.Length != rightNumber.Length
                            ? leftNumber.Length.CompareTo(rightNumber.Length)
                            : StringComparer.Ordinal.Compare(leftNumber, rightNumber);
                    }
                    else
                    {
                        comparison = _compareInfo.Compare(
                            leftPart,
                            rightPart,
                            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);
                    }

                    if (comparison != 0) return comparison;
                }

                return leftParts.Length.CompareTo(rightParts.Length);
            }
        }

        public sealed record SyncResult(int Added, int Updated, int Preserved, int DuplicatesRemoved);
    }
}
