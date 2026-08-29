namespace coreAPI.Models
{
    public class M3UExportViewModel
    {
        public List<ServerMapping> Servers { get; set; } = new();

        public string? SelectedServerId { get; set; }

        public string? ErrorMessage { get; set; }
    }
}
