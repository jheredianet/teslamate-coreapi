using Microsoft.AspNetCore.Mvc;
using coreAPI.Classes;
using coreAPI.Models;
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
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Import(string htmlContent)
        {
            if (string.IsNullOrWhiteSpace(htmlContent))
            {
                TempData["Message"] = "No se ha introducido contenido para importar.";
                return RedirectToAction(nameof(Index));
            }

            var entries = _service.LoadEntries();

            // Usamos HtmlAgilityPack para parsear HTML
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(htmlContent);

            var anchors = doc.DocumentNode.SelectNodes("//a[@href]");
            if (anchors != null)
            {
                foreach (var a in anchors)
                {
                    var href = a.GetAttributeValue("href", "").Trim();
                    if (string.IsNullOrWhiteSpace(href)) continue;

                    // Texto visible sin etiquetas internas
                    var channelName = HtmlAgilityPack.HtmlEntity.DeEntitize(a.InnerText).Trim();

                    // Crear nueva entrada
                    var newEntry = new M3UEntry
                    {
                        Id = entries.Count > 0 ? entries.Max(e => e.Id) + 1 : 0,
                        Order = entries.Count > 0 ? entries.Max(e => e.Order) + 1 : 0,
                        GroupTitle = "Otros",
                        TVGLogo = "https://listaiptvtelevision.com/wp-content/uploads/m3u.png",
                        ChannelName = channelName,
                        StreamUrl = href
                    };

                    entries.Add(newEntry);
                }

                // Eliminar duplicados por StreamUrl
                entries = entries
                    .GroupBy(e => e.StreamUrl.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(e => e.Order)
                    .ToList();

                _service.SaveEntries(entries);
                TempData["Message"] = "Importación completada.";
            }
            else
            {
                TempData["Message"] = "No se encontraron enlaces en el HTML.";
            }

            return RedirectToAction(nameof(Index));
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
