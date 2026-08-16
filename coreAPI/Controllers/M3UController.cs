using Microsoft.AspNetCore.Mvc;
using coreAPI.Classes;
using coreAPI.Models;
using System.Net;
using System.Text;
using System.Text.Json;

namespace coreAPI.Controllers
{
    public class M3UController : Controller
    {
        private readonly M3UService _service;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IWebHostEnvironment _env;
        private Dictionary<string, string> _serverMappings;
        private readonly string _userIdPath;

        private const string MonitoringServerUrl = "https://jchmip.infoinnova.net:444";

        public M3UController(M3UService service, IHttpClientFactory httpClientFactory, IWebHostEnvironment env)
        {
            _service = service;
            _httpClientFactory = httpClientFactory;
            _env = env;
            _userIdPath = Path.Combine(env.ContentRootPath, "import", "userid.json");
            _serverMappings = LoadServerMappings();
        }

        private Dictionary<string, string> LoadServerMappings()
        {
            if (!System.IO.File.Exists(_userIdPath))
            {
                return new Dictionary<string, string>();
            }

            try
            {
                var json = System.IO.File.ReadAllText(_userIdPath, Encoding.UTF8);
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        public void ReloadServerMappings()
        {
            _serverMappings = LoadServerMappings();
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
            entry.Id = entries.Count;
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

            _service.SaveEntries(cleaned);

            TempData["Message"] = "Lista depurada: duplicados eliminados y valores por defecto aplicados.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> ValidateChannels()
        {
            var entries = _service.LoadEntries();
            var model = new M3UValidationViewModel();
            using var semaphore = new SemaphoreSlim(5);

            var validations = entries.Select(async entry =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await ValidateChannelAsync(entry);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            model.Results = (await Task.WhenAll(validations))
                .OrderBy(r => r.Id)
                .ToList();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteValidated(List<int> selectedIds)
        {
            if (selectedIds.Count == 0)
            {
                TempData["Message"] = "No se seleccionaron canales para eliminar.";
                return RedirectToAction(nameof(ValidateChannels));
            }

            var entries = _service.LoadEntries();
            var selected = selectedIds.ToHashSet();
            var remaining = entries.Where(e => !selected.Contains(e.Id)).ToList();
            var deletedCount = entries.Count - remaining.Count;

            _service.SaveEntries(remaining);
            TempData["Message"] = $"Se eliminaron {deletedCount} canales.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<M3UValidationResult> ValidateChannelAsync(M3UEntry entry)
        {
            var result = new M3UValidationResult
            {
                Id = entry.Id,
                ChannelName = entry.ChannelName,
                StreamUrl = entry.StreamUrl
            };

            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(8);

                if (TryGetAceIdentifier(entry.StreamUrl, out var parameterName, out var identifier))
                {
                    var apiUrl = $"{MonitoringServerUrl}/server/api?api_version=3&method=get_media_files&{parameterName}={Uri.EscapeDataString(identifier)}";
                    using var response = await client.GetAsync(apiUrl);
                    var body = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode &&
                        TryGetApiResult(body, out var apiResult))
                    {
                        var name = GetJsonString(apiResult, "name");
                        var type = GetJsonString(apiResult, "type");
                        result.Status = "Online";
                        result.Details = string.IsNullOrWhiteSpace(name)
                            ? $"Ace Stream disponible{FormatType(type)}"
                            : $"{name}{FormatType(type)}";
                        return result;
                    }

                    result.Status = "Offline";
                    result.Details = "Ace Stream no disponible o sin metadatos.";
                    result.SuggestDelete = true;
                    return result;
                }

                if (Uri.TryCreate(entry.StreamUrl, UriKind.Absolute, out var uri) &&
                    (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                     uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                {
                    using var request = new HttpRequestMessage(HttpMethod.Head, uri);
                    using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                    if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
                    {
                        using var fallback = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
                        result.Status = IsSuccessful(fallback.StatusCode) ? "Online" : "Offline";
                        result.Details = $"HTTP {(int)fallback.StatusCode}";
                    }
                    else
                    {
                        result.Status = IsSuccessful(response.StatusCode) ? "Online" : "Offline";
                        result.Details = $"HTTP {(int)response.StatusCode}";
                    }

                    result.SuggestDelete = result.Status == "Offline";
                    return result;
                }

                result.Status = "No verificable";
                result.Details = "Protocolo no soportado.";
                return result;
            }
            catch (TaskCanceledException)
            {
                result.Status = "Offline";
                result.Details = "Tiempo de espera agotado.";
                result.SuggestDelete = true;
                return result;
            }
            catch (HttpRequestException ex)
            {
                result.Status = "No verificable";
                result.Details = ex.Message;
                return result;
            }
            catch (JsonException)
            {
                result.Status = "No verificable";
                result.Details = "Respuesta inválida del servidor.";
                return result;
            }
        }

        private static bool TryGetAceIdentifier(string? streamUrl, out string parameterName, out string identifier)
        {
            parameterName = string.Empty;
            identifier = string.Empty;

            if (string.IsNullOrWhiteSpace(streamUrl)) return false;
            var value = streamUrl.Trim();

            if (value.StartsWith("acestream://", StringComparison.OrdinalIgnoreCase))
            {
                parameterName = "content_id";
                identifier = value["acestream://".Length..].Trim();
                return !string.IsNullOrWhiteSpace(identifier);
            }

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
            if (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;

            var infohash = GetQueryParameter(uri.Query.TrimStart('?'), "infohash");
            if (!string.IsNullOrWhiteSpace(infohash))
            {
                parameterName = "infohash";
                identifier = infohash;
                return true;
            }

            return false;
        }

        private static bool TryGetApiResult(string body, out JsonElement result)
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("result", out result) && result.ValueKind == JsonValueKind.Object)
            {
                result = result.Clone();
                return true;
            }

            result = default;
            return false;
        }

        private static string? GetJsonString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static string FormatType(string? type)
        {
            return string.IsNullOrWhiteSpace(type) ? string.Empty : $" ({type})";
        }

        private static bool IsSuccessful(HttpStatusCode statusCode)
        {
            return (int)statusCode >= 200 && (int)statusCode < 400;
        }


        // Export rápido (por si quieres descargar la lista)
        public IActionResult Download()
        {
            var bytes = System.IO.File.ReadAllBytes(HttpContext.RequestServices
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<M3UOptions>>().Value.FilePath);
            return File(bytes, "application/x-mpegURL", "lista.m3u");
        }

        [HttpGet]
        public IActionResult Import()
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

            if (string.IsNullOrWhiteSpace(model.SearchQuery))
            {
                model.ErrorMessage = "Introduce un texto para buscar.";
                return View("Import", model);
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var searchQueryEncoded = Uri.EscapeDataString(model.SearchQuery);
                var searchUrl = $"{MonitoringServerUrl}/search.m3u?query={searchQueryEncoded}";
                var searchString = await client.GetStringAsync(searchUrl);
                var searchResults = _service.ParseEntries(searchString);
                model.Results = await ConvertInfohashesToContentIdsAsync(client, searchResults);

                if (model.Results.Count == 0)
                    model.ErrorMessage = "La búsqueda no ha devuelto canales válidos en formato M3U.";
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is InvalidOperationException || ex is JsonException)
            {
                model.ErrorMessage = $"Error al conectar con el servidor de monitorización: {ex.Message}";
            }

            return View("Import", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Import(M3USearchViewModel model)
        {
            if (model.Results.Count == 0)
            {
                TempData["Message"] = "No hay resultados para importar.";
                return RedirectToAction(nameof(Import));
            }

            var entries = _service.LoadEntries();
            var nextOrder = entries.Count > 0 ? entries.Max(e => e.Order) + 1 : 0;

            foreach (var result in model.Results)
            {
                if (string.IsNullOrWhiteSpace(result.StreamUrl)) continue;

                entries.Add(new M3UEntry
                {
                    GroupTitle = string.IsNullOrWhiteSpace(result.GroupTitle) ? "Otros" : result.GroupTitle,
                    TVGLogo = string.IsNullOrWhiteSpace(result.TVGLogo)
                        ? "https://listaiptvtelevision.com/wp-content/uploads/m3u.png"
                        : result.TVGLogo,
                    TVGId = result.TVGId,
                    ChannelName = string.IsNullOrWhiteSpace(result.ChannelName) ? "Unknown" : result.ChannelName,
                    StreamUrl = result.StreamUrl.Trim(),
                    Order = nextOrder++
                });
            }

            entries = entries
                .GroupBy(e => e.StreamUrl.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(e => e.Order)
                .ToList();

            _service.SaveEntries(entries);
            TempData["Message"] = $"Importación completada: {model.Results.Count} resultados procesados.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<M3UEntry>> ConvertInfohashesToContentIdsAsync(
            HttpClient client,
            List<M3UEntry> results)
        {
            var convertedResults = new List<M3UEntry>();

            foreach (var result in results)
            {
                var infohash = ExtractInfohash(result.StreamUrl);
                if (string.IsNullOrWhiteSpace(infohash))
                {
                    convertedResults.Add(result);
                    continue;
                }

                try
                {
                    var contentId = await GetContentIdAsync(client, infohash);
                    result.StreamUrl = $"acestream://{contentId}";
                    convertedResults.Add(result);
                }
                catch (InvalidOperationException)
                {
                    // El contenido puede haber desaparecido del motor. No se muestra
                    // ni se importa un resultado que siga apuntando al infohash.
                }
            }

            return convertedResults;
        }

        private async Task<string> GetContentIdAsync(HttpClient client, string infohash)
        {
            var encodedInfohash = Uri.EscapeDataString(infohash);
            var apiUrl = $"{MonitoringServerUrl}/server/api?api_version=3&method=get_content_id&infohash={encodedInfohash}";
            using var response = await client.GetAsync(apiUrl);
            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync();
            using var json = await JsonDocument.ParseAsync(responseStream);

            if (json.RootElement.TryGetProperty("result", out var result) &&
                result.TryGetProperty("content_id", out var contentId) &&
                !string.IsNullOrWhiteSpace(contentId.GetString()))
            {
                return contentId.GetString()!;
            }

            throw new InvalidOperationException($"No se pudo obtener content_id para el infohash {infohash}.");
        }

        private static string? ExtractInfohash(string? streamUrl)
        {
            if (string.IsNullOrWhiteSpace(streamUrl))
                return null;

            var value = streamUrl.Trim();

            // El buscador devuelve normalmente una URL como:
            // https://servidor/ace/getstream?infohash=...
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                !string.IsNullOrWhiteSpace(uri.Query))
            {
                var infohashFromUrl = GetQueryParameter(uri.Query.TrimStart('?'), "infohash");
                if (!string.IsNullOrWhiteSpace(infohashFromUrl))
                    return infohashFromUrl;
            }

            if (value.StartsWith("acestream://", StringComparison.OrdinalIgnoreCase))
            {
                var infohash = value["acestream://".Length..].Trim();
                return infohash.Contains('=') ? null : infohash;
            }

            if (value.StartsWith("acestream:?", StringComparison.OrdinalIgnoreCase))
            {
                var query = value["acestream:?".Length..];
                return GetQueryParameter(query, "infohash");
            }

            if (value.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                var query = value["magnet:?".Length..];
                var xt = GetQueryParameter(query, "xt");
                const string prefix = "urn:btih:";
                return xt?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true
                    ? xt[prefix.Length..]
                    : null;
            }

            return null;
        }

        private static string? GetQueryParameter(string query, string parameterName)
        {
            foreach (var part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separator = part.IndexOf('=');
                if (separator <= 0) continue;

                var name = Uri.UnescapeDataString(part[..separator]);
                if (!name.Equals(parameterName, StringComparison.OrdinalIgnoreCase)) continue;

                return Uri.UnescapeDataString(part[(separator + 1)..]);
            }

            return null;
        }

        /// <summary>
        /// Endpoint que devuelve un fichero M3U dinámico procesado según el id proporcionado.
        /// Replica la funcionalidad del script PHP original.
        /// </summary>
        /// <param name="id">Identificador del servidor de destino (se lee del fichero import/listam3u)</param>
        /// <returns>Contenido M3U con content-type audio/x-mpegurl</returns>
        [HttpGet("stream")]
        public async Task<IActionResult> Stream(string? id)
        {
            if (string.IsNullOrWhiteSpace(id) || !_serverMappings.TryGetValue(id, out var userIp))
            {
                return BadRequest("Parámetro 'id' requerido. Valores válidos: " + string.Join(", ", _serverMappings.Keys));
            }

            try
            {
                var client = _httpClientFactory.CreateClient();

                // 1. Leer fichero local custom_ace.txt
                var customAcePath = Path.Combine(_env.ContentRootPath, "import", "lista.m3u");
                var customUrls = string.Empty;
                if (System.IO.File.Exists(customAcePath))
                {
                    customUrls = await System.IO.File.ReadAllTextAsync(customAcePath, Encoding.UTF8);
                }

                // 2. Obtener búsqueda del servidor de monitorización
                var searchQuery = "liga%20OR%20campeones%20OR%20dazn%20OR%20madrid%20OR%20copa%20OR%20vamos%20OR%20deportes%20OR%20nba%20OR%20espn%20OR%20eurosport%20OR%20Movistar";
                var searchUrl = $"{MonitoringServerUrl}/search.m3u?query={searchQuery}";

                var searchString = await client.GetStringAsync(searchUrl);

                // 3. Eliminar #EXTM3U extra del contenido remoto
                searchString = searchString.Replace("#EXTM3U", "", StringComparison.OrdinalIgnoreCase);

                // 4. Concatenar contenidos (customUrls + searchString)
                var vdata = customUrls + searchString;

                // 5. Reemplazar acestream:// por URL del usuario
                vdata = vdata.Replace("acestream://", $"{userIp}/ace/getstream?id=");

                // 6. Reemplazar URL del servidor de monitorización por userIp
                vdata = vdata.Replace(MonitoringServerUrl, userIp);

                // 7. Eliminar canales ELCANO (case insensitive) y la línea siguiente
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

                vdata = string.Join('\n', filteredLines);

                // 8. Devolver con content-type M3U
                return Content(vdata, "audio/x-mpegurl", Encoding.UTF8);
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(502, $"Error al conectar con el servidor de monitorización: {ex.Message}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno: {ex.Message}");
            }
        }

    }
}
