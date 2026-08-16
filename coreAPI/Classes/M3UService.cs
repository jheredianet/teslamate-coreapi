using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using coreAPI.Models;

namespace coreAPI.Classes
{
    public class M3UService
    {
        private readonly string _filePath;
        private readonly int _backupRetention;
        private static readonly object _fileLock = new();

        private static readonly Regex GroupRegex = new(@"group-title=""(.*?)""", RegexOptions.Compiled);
        private static readonly Regex LogoRegex = new(@"tvg-logo=""(.*?)""", RegexOptions.Compiled);

        public M3UService(IOptions<M3UOptions> options)
        {
            _filePath = options.Value.FilePath;
            _backupRetention = Math.Max(0, options.Value.BackupRetention);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_filePath))!);
            if (!File.Exists(_filePath))
                File.WriteAllText(_filePath, "#EXTM3U\n", Encoding.UTF8);
        }

        public List<M3UEntry> LoadEntries()
        {
            lock (_fileLock)
            {
                return ParseEntries(File.ReadAllText(_filePath, Encoding.UTF8));
            }
        }

        public List<M3UEntry> ParseEntries(string content)
        {
            var lines = content.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries);

            var entries = new List<M3UEntry>();
            int order = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                // Saltar cabecera y comentarios
                if (line.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
                {
                    // La siguiente línea debe ser la URL
                    if (i + 1 >= lines.Length)
                        break;

                    var meta = line;
                    var url = lines[i + 1].Trim();
                    i++; // avanzamos el índice porque consumimos la URL

                    var group = GroupRegex.Match(meta);
                    var logo = LogoRegex.Match(meta);

                    var namePartIndex = meta.IndexOf(',', StringComparison.Ordinal);
                    var channelName = namePartIndex >= 0 ? meta[(namePartIndex + 1)..].Trim() : "Unknown";

                    entries.Add(new M3UEntry
                    {
                        Id = order,
                        Order = order,
                        GroupTitle = group.Success ? group.Groups[1].Value : "",
                        TVGLogo = logo.Success ? logo.Groups[1].Value : null,
                        ChannelName = channelName,
                        StreamUrl = url
                    });
                    order++;
                }
                // Si aparecen líneas que no son EXTINF ni URL, se ignoran
            }

            return entries;
        }

        public void SaveEntries(List<M3UEntry> entries)
        {
            lock (_fileLock)
            {
                // Normalizar orden consecutivo
                var ordered = entries.OrderBy(e => e.Order).ToList();
                for (int i = 0; i < ordered.Count; i++)
                {
                    ordered[i].Order = i;
                    ordered[i].Id = i;
                }

                // Backup antes de escribir
                CreateBackup();

                var sb = new StringBuilder();
                sb.AppendLine("#EXTM3U");
                foreach (var e in ordered)
                {
                    var logo = string.IsNullOrWhiteSpace(e.TVGLogo) ? "" : $" tvg-logo=\"{e.TVGLogo}\"";
                    var tvgId = string.IsNullOrWhiteSpace(e.TVGId) ? $" tvg-id=\"{e.GroupTitle}\"" : $" tvg-id=\"{e.TVGId}\"";
                    var group = string.IsNullOrWhiteSpace(e.GroupTitle) ? "Otros" : e.GroupTitle;

                    sb.AppendLine($"#EXTINF:-1{logo}{tvgId} group-title=\"{group}\", {e.ChannelName}");
                    sb.AppendLine(e.StreamUrl);
                }

                // Escritura atómica
                var tmp = _filePath + ".tmp";
                File.WriteAllText(tmp, sb.ToString(), Encoding.UTF8);
                File.Copy(tmp, _filePath, overwrite: true);
                File.Delete(tmp);
            }
        }

        public void MoveUp(int id)
        {
            var entries = LoadEntries();
            var idx = entries.FindIndex(x => x.Id == id);
            if (idx > 0)
            {
                (entries[idx - 1].Order, entries[idx].Order) = (entries[idx].Order, entries[idx - 1].Order);
                SaveEntries(entries);
            }
        }

        public void MoveDown(int id)
        {
            var entries = LoadEntries();
            var idx = entries.FindIndex(x => x.Id == id);
            if (idx >= 0 && idx < entries.Count - 1)
            {
                (entries[idx + 1].Order, entries[idx].Order) = (entries[idx].Order, entries[idx + 1].Order);
                SaveEntries(entries);
            }
        }

        private void CreateBackup()
        {
            if (!File.Exists(_filePath)) return;
            var dir = Path.GetDirectoryName(_filePath)!;
            var name = Path.GetFileNameWithoutExtension(_filePath);
            var ext = Path.GetExtension(_filePath);

            var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backup = Path.Combine(dir, $"{name}.{stamp}{ext}.bak");
            File.Copy(_filePath, backup, overwrite: true);

            // Retención
            if (_backupRetention > 0)
            {
                var backups = Directory.GetFiles(dir, $"{name}.*{ext}.bak")
                                       .OrderByDescending(f => f)
                                       .ToList();
                foreach (var old in backups.Skip(_backupRetention))
                    File.Delete(old);
            }
        }
    }
}
