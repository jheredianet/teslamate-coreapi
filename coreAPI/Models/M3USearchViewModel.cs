namespace coreAPI.Models
{
    public class M3USearchViewModel
    {
        public string SearchQuery { get; set; } = string.Empty;

        public List<M3UEntry> Results { get; set; } = new();

        public string? ErrorMessage { get; set; }
    }
}
