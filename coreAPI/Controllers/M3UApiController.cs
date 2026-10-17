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
        private readonly IHttpClientFactory _httpClientFactory;

        public M3UApiController(M3UService service, IHttpClientFactory httpClientFactory)
        {
            _service = service;
            _httpClientFactory = httpClientFactory;
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

            return Ok(entries);
        }

        [HttpGet("{id:int}")]
        public ActionResult<M3UEntry> GetById(int id)
        {
            var entry = _service.LoadEntries().FirstOrDefault(e => e.Id == id);
            return entry == null ? NotFound() : Ok(entry);
        }

        [HttpPost("synchronize")]
        public async Task<ActionResult<M3UService.SyncResult>> Synchronize(CancellationToken cancellationToken)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);
                var result = await _service.SynchronizeAsync(client, cancellationToken);
                return Ok(result);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return StatusCode(StatusCodes.Status504GatewayTimeout,
                    "La fuente M3U no respondió dentro del tiempo límite.");
            }
            catch (HttpRequestException ex)
            {
                return StatusCode(StatusCodes.Status502BadGateway,
                    $"No se pudo consultar la fuente M3U: {ex.Message}");
            }
            catch (InvalidOperationException ex)
            {
                return UnprocessableEntity(ex.Message);
            }
        }

        [HttpPost]
        public ActionResult<M3UEntry> Create([FromBody] M3UEntry entry)
        {
            var entries = _service.LoadEntries();
            entry.Id = 0;

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

    }
}
