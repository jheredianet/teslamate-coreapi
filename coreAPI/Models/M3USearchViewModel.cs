namespace coreAPI.Models
{
    public class M3USearchViewModel
    {
        public string SearchQuery { get; set; } = string.Empty;

        public string? RawJson { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
