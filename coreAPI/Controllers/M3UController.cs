using Microsoft.AspNetCore.Mvc;
using coreAPI.Classes;
using coreAPI.Models;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace coreAPI.Controllers
{
    public class M3UController : Controller
    {
        private readonly M3UService _service;
        private readonly ServerMappingService _serverMappingService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<M3UOptions> _m3uOptions;

        private const string MonitoringServerUrl = "https://jchmip.infoinnova.net:444";
        private static readonly Uri MonitoringServerUri = new(MonitoringServerUrl);

        public M3UController(
            M3UService service,
            ServerMappingService serverMappingService,
            IHttpClientFactory httpClientFactory,
            IOptions<M3UOptions> m3uOptions)
        {
            _service = service;
            _serverMappingService = serverMappingService;
            _httpClientFactory = httpClientFactory;
            _m3uOptions = m3uOptions;
        }

        public IActionResult Index(string? q, string? group)
        {
            var list = _service.LoadEntries();

            if (!string.IsNullOrWhiteSpace(group))
                list = list.Where(e => string.Equals(e.GroupTitle, group, StringComparison.OrdinalIgnoreCase)).ToList();

            if (!string.IsNullOrWhiteSpace(q))
                list = list.Where(e => e.ChannelName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                       e.StreamUrl.Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();

            var groups = _service.LoadEntries()
                                 .Select(e => e.GroupTitle)
                                 .Distinct()
                                 .OrderBy(g => g)
                                 .ToList();

            ViewBag.Groups = groups;
            ViewBag.Query = q;
            ViewBag.SelectedGroup = group;

            return View(list.OrderBy(e => e.Order).ToList());
        }

        public IActionResult Create()
        {
            return View(new M3UEntry());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(M3UEntry entry)
        {
            if (!ModelState.IsValid) return View(entry);

            var entries = _service.LoadEntries();
            entry.Id = 0;
            entry.Order = entries.Count;
            entries.Add(entry);
            _service.SaveEntries(entries);
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Edit(int id)
        {
            var entry = _service.LoadEntries().FirstOrDefault(e => e.Id == id);
            if (entry == null) return NotFound();
            return View(entry);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(M3UEntry updated)
        {
            if (!ModelState.IsValid) return View(updated);

            var entries = _service.LoadEntries();
            var idx = entries.FindIndex(e => e.Id == updated.Id);
            if (idx < 0) return NotFound();

            // Mantener Order
            updated.Order = entries[idx].Order;
            entries[idx] = updated;
            _service.SaveEntries(entries);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var entries = _service.LoadEntries();
            var removed = entries.RemoveAll(e => e.Id == id);
            if (removed > 0)
                _service.SaveEntries(entries);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MoveUp(int id)
        {
            _service.MoveUp(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MoveDown(int id)
        {
            _service.MoveDown(id);
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reorder(List<int> orderedIds)
        {
            if (orderedIds.Count > 0)
                _service.Reorder(orderedIds);

            return Ok();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CleanDuplicates()
        {
            var entries = _service.LoadEntries();

            // Normalizar valores por defecto
            foreach (var e in entries)
            {
                if (string.IsNullOrWhiteSpace(e.GroupTitle))
                    e.GroupTitle = "Otros";

                if (string.IsNullOrWhiteSpace(e.TVGLogo))
                    e.TVGLogo = "https://listaiptvtelevision.com/wp-content/uploads/m3u.png";
            }

            // Eliminar duplicados por StreamUrl (manteniendo el primero)
            var cleaned = entries
                .GroupBy(e => e.StreamUrl.Trim(), StringComparer.CurrentCulture)
                .Select(g => g.First())
                .OrderBy(e => e.Order)
                .ToList();

            // Consolidar la posición visible en un orden consecutivo antes de persistir.
            for (var i = 0; i < cleaned.Count; i++)
                cleaned[i].Order = i;

            _service.SaveEntries(cleaned);

            TempData["Message"] = "Lista depurada y consolidada: duplicados eliminados, orden conservado y valores por defecto aplicados.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Servers()
        {
            return View(_serverMappingService.LoadAll());
        }

        [HttpGet]
        public IActionResult CreateServer()
        {
            return View(new ServerMapping());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateServer(ServerMapping mapping)
        {
            if (!ModelState.IsValid)
                return View(mapping);

            try
            {
                _serverMappingService.Create(mapping);
                TempData["Message"] = "Servidor guardado correctamente.";
                return RedirectToAction(nameof(Servers));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(ServerMapping.Name), ex.Message);
                return View(mapping);
            }
        }

        [HttpGet]
        public IActionResult EditServer(int id)
        {
            var mapping = _serverMappingService.GetById(id);
            return mapping == null ? NotFound() : View(mapping);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditServer(ServerMapping mapping)
        {
            if (!ModelState.IsValid)
                return View(mapping);

            try
            {
                if (!_serverMappingService.Update(mapping))
                    return NotFound();

                TempData["Message"] = "Servidor actualizado correctamente.";
                return RedirectToAction(nameof(Servers));
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(nameof(ServerMapping.Name), ex.Message);
                return View(mapping);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteServer(int id)
        {
            if (_serverMappingService.Delete(id))
                TempData["Message"] = "Servidor eliminado correctamente.";
            else
                TempData["Message"] = "El servidor no existe.";

            return RedirectToAction(nameof(Servers));
        }

        [HttpGet]
        public async Task<IActionResult> Info(int id)
        {
            var entry = _service.LoadEntries().FirstOrDefault(e => e.Id == id);
            if (entry == null)
                return NotFound();

            var model = new M3UInfoViewModel
            {
                Entry = entry
            };

            const string aceStreamPrefix = "acestream://";
            if (!entry.StreamUrl.StartsWith(aceStreamPrefix, StringComparison.OrdinalIgnoreCase))
            {
                model.ErrorMessage = "La información detallada solo está disponible para canales acestream://.";
                return View(model);
            }

            var contentId = entry.StreamUrl[aceStreamPrefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(contentId))
            {
                model.ErrorMessage = "El canal no contiene un content_id válido.";
                return View(model);
            }

            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                var encodedContentId = Uri.EscapeDataString(contentId);
                var sessionUrl = $"{MonitoringServerUrl}/ace/getstream?content_id={encodedContentId}&format=json";
                var playbackResponse = await client.GetStringAsync(sessionUrl);
                model.PlaybackJson = PrettyJson(playbackResponse);

                using var playbackDocument = JsonDocument.Parse(playbackResponse);
                if (playbackDocument.RootElement.TryGetProperty("response", out var response))
                {
                    var statUrl = response.TryGetProperty("stat_url", out var statUrlElement) &&
                                  statUrlElement.ValueKind == JsonValueKind.String
                        ? statUrlElement.GetString()
                        : null;

                    if (!string.IsNullOrWhiteSpace(statUrl))
                    {
                        // Algunos motores devuelven stat_url en HTTP aunque la URL
                        // pública del servicio esté configurada con HTTPS.
                        var normalizedStatUrl = NormalizeMonitoringUrl(statUrl);

                        // Esperar unos segundos da tiempo a que aparezcan peers y
                        // velocidad de descarga en la sesión.
                        for (var attempt = 0; attempt < 5; attempt++)
                        {
                            var statsResponse = await client.GetStringAsync(normalizedStatUrl);
                            model.StatsJson = PrettyJson(statsResponse);

                            using var statsDocument = JsonDocument.Parse(statsResponse);
                            if (HasActivePeerStats(statsDocument.RootElement) || attempt == 4)
                                break;

                            await Task.Delay(TimeSpan.FromSeconds(1));
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is InvalidOperationException || ex is JsonException || ex is TaskCanceledException)
            {
                model.ErrorMessage = $"No se pudo obtener la información del canal: {ex.Message}";
            }

            return View(model);
        }

        private static string PrettyJson(string json)
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        private static bool HasActivePeerStats(JsonElement root)
        {
            if (!root.TryGetProperty("response", out var response))
                return false;

            if (response.TryGetProperty("peers", out var peers) &&
                peers.ValueKind == JsonValueKind.Number)
                return true;

            return response.TryGetProperty("status", out var status) &&
                   status.ValueKind == JsonValueKind.String &&
                   !string.Equals(status.GetString(), "idle", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeMonitoringUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var returnedUri))
                throw new InvalidOperationException("El motor devolvió una stat_url no válida.");

            var builder = new UriBuilder(returnedUri)
            {
                Scheme = MonitoringServerUri.Scheme,
                Host = MonitoringServerUri.Host,
                Port = MonitoringServerUri.Port
            };

            return builder.Uri.ToString();
        }


        [HttpGet]
        public IActionResult Download()
        {
            return View(new M3UExportViewModel
            {
                Servers = _serverMappingService.LoadAll()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Download(string serverId)
        {
            var model = new M3UExportViewModel
            {
                Servers = _serverMappingService.LoadAll(),
                SelectedServerId = serverId
            };

            try
            {
                var content = await BuildStreamContentAsync(serverId, "mpegts");
                return File(Encoding.UTF8.GetBytes(content), "audio/x-mpegurl", "lista.m3u");
            }
            catch (ArgumentException ex)
            {
                model.ErrorMessage = ex.Message;
                return View(model);
            }
            catch (Exception ex)
            {
                model.ErrorMessage = $"No se pudo generar la lista: {ex.Message}";
                return View(model);
            }
        }

        [HttpGet]
        public IActionResult Search()
        {
            return View(new M3USearchViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Search(string searchQuery)
        {
            var model = new M3USearchViewModel
            {
                SearchQuery = searchQuery?.Trim() ?? string.Empty
            };

            var terms = model.SearchQuery
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (terms.Length == 0)
            {
                model.ErrorMessage = "Introduce al menos un término de búsqueda. Puedes separar varios términos con comas.";
                return View(model);
            }

            model.SearchQuery = string.Join(" OR ", terms);

            try
            {
                var client = _httpClientFactory.CreateClient();
                var searchQueryEncoded = Uri.EscapeDataString(model.SearchQuery);
                var searchUrl = $"{MonitoringServerUrl}/search.m3u?query={searchQueryEncoded}";
                var searchString = await client.GetStringAsync(searchUrl);

                using var document = JsonDocument.Parse(searchString);
                model.RawJson = JsonSerializer.Serialize(
                    document.RootElement,
                    new JsonSerializerOptions { WriteIndented = true });
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is InvalidOperationException || ex is JsonException)
            {
                model.ErrorMessage = $"Error al realizar la búsqueda: {ex.Message}";
            }

            return View(model);
        }

        /// <summary>
        /// Endpoint que devuelve un fichero M3U dinámico procesado según el id proporcionado.
        /// Replica la funcionalidad del script PHP original.
        /// </summary>
        /// <param name="id">Identificador del servidor de destino (se lee del fichero import/listam3u)</param>
        /// <param name="format">Formato de salida opcional: mpegts (predeterminado) o hls</param>
        /// <returns>Contenido M3U con content-type audio/x-mpegurl</returns>
        [HttpGet("stream")]
        public async Task<IActionResult> Stream(string? id, string? format = null)
        {
            var outputFormat = string.IsNullOrWhiteSpace(format)
                ? "mpegts"
                : format.Trim().ToLowerInvariant();

            if (outputFormat is not "mpegts" and not "hls")
            {
                return BadRequest("Parámetro 'format' no válido. Valores permitidos: mpegts o hls.");
            }

            try
            {
                var vdata = await BuildStreamContentAsync(id, outputFormat);
                return Content(vdata, "audio/x-mpegurl", Encoding.UTF8);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

        private async Task<string> BuildStreamContentAsync(string? id, string outputFormat)
        {
            var serverMappings = _serverMappingService.LoadDictionary();
            if (string.IsNullOrWhiteSpace(id) || !serverMappings.TryGetValue(id, out var userIp))
            {
                throw new ArgumentException(
                    "Parámetro 'id' requerido. Valores válidos: " + string.Join(", ", serverMappings.Keys));
            }

            var customAcePath = _m3uOptions.Value.FilePath;
            var vdata = await System.IO.File.ReadAllTextAsync(customAcePath, Encoding.UTF8);

            var aceStreamUrl = outputFormat == "hls"
                ? $"{userIp}/ace/manifest.m3u8?id="
                : $"{userIp}/ace/getstream?id=";
            vdata = vdata.Replace("acestream://", aceStreamUrl);
            vdata = vdata.Replace(MonitoringServerUrl, userIp);

            var lines = vdata.Split('\n');
            var filteredLines = new List<string>();
            var skipNext = false;

            foreach (var line in lines)
            {
                if (skipNext)
                {
                    skipNext = false;
                    continue;
                }

                if (line.Contains("ELCANO", StringComparison.OrdinalIgnoreCase))
                {
                    skipNext = true;
                    continue;
                }

                filteredLines.Add(line);
            }

            return string.Join('\n', filteredLines);
        }

    }
}
