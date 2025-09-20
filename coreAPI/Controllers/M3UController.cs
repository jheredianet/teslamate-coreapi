using Microsoft.AspNetCore.Mvc;
using coreAPI.Classes;
using coreAPI.Models;

namespace coreAPI.Controllers
{
    public class M3UController : Controller
    {
        private readonly M3UService _service;

        public M3UController(M3UService service)
        {
            _service = service;
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

    }
}
