namespace coreAPI.Models
{
    public class M3UInfoViewModel
    {
        public M3UEntry Entry { get; set; } = new();

        public string? PlaybackJson { get; set; }

        public string? StatsJson { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
