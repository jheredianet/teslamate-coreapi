namespace coreAPI.Models
{
    public class M3UExportViewModel
    {
        public List<ServerMapping> Servers { get; set; } = new();

        public string? SelectedServerId { get; set; }

        public string Format { get; set; } = "mpegts";

        public string? ErrorMessage { get; set; }
    }
}
