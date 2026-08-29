using coreAPI.Classes;
using coreAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace coreAPI.Controllers
{
    [ApiController]
    [Route("api/m3u")]
    public class M3UApiController : ControllerBase
    {
        private readonly M3UService _service;

        public M3UApiController(M3UService service)
        {
            _service = service;
        }

        [HttpGet]
        public ActionResult<IEnumerable<M3UEntry>> GetAll(
            [FromQuery] string? q = null,
            [FromQuery] string? group = null)
        {
            var entries = _service.LoadEntries();

            if (!string.IsNullOrWhiteSpace(q))
            {
                entries = entries
                    .Where(e => e.ChannelName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                e.StreamUrl.Contains(q, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (!string.IsNullOrWhiteSpace(group))
            {
                entries = entries
                    .Where(e => string.Equals(e.GroupTitle, group, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            return Ok(entries.OrderBy(e => e.Order));
        }

        [HttpGet("{id:int}")]
        public ActionResult<M3UEntry> GetById(int id)
        {
            var entry = _service.LoadEntries().FirstOrDefault(e => e.Id == id);
            return entry == null ? NotFound() : Ok(entry);
        }

        [HttpPost]
        public ActionResult<M3UEntry> Create([FromBody] M3UEntry entry)
        {
            var entries = _service.LoadEntries();
            entry.Id = 0;
            entry.Order = entries.Count > 0 ? entries.Max(e => e.Order) + 1 : 0;

            entries.Add(entry);
            _service.SaveEntries(entries);

            return CreatedAtAction(nameof(GetById), new { id = entry.Id }, entry);
        }

        [HttpPut("{id:int}")]
        public ActionResult<M3UEntry> Update(int id, [FromBody] M3UEntry updated)
        {
            var entries = _service.LoadEntries();
            var index = entries.FindIndex(e => e.Id == id);
            if (index < 0) return NotFound();

            updated.Id = id;
            updated.Order = entries[index].Order;
            entries[index] = updated;
            _service.SaveEntries(entries);

            return Ok(updated);
        }

        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            var entries = _service.LoadEntries();
            var removed = entries.RemoveAll(e => e.Id == id);
            if (removed == 0) return NotFound();

            _service.SaveEntries(entries);
            return NoContent();
        }

        [HttpPost("reorder")]
        public IActionResult Reorder([FromBody] List<int> orderedIds)
        {
            if (orderedIds.Count == 0)
                return BadRequest("Debe indicar al menos un identificador.");

            _service.Reorder(orderedIds);
            return NoContent();
        }
    }
}
