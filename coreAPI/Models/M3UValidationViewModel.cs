namespace coreAPI.Models
{
    public class M3UValidationViewModel
    {
        public List<M3UValidationResult> Results { get; set; } = new();

        public int SuggestedCount => Results.Count(r => r.SuggestDelete);
    }

    public class M3UValidationResult
    {
        public int Id { get; set; }
        public string ChannelName { get; set; } = string.Empty;
        public string StreamUrl { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public bool SuggestDelete { get; set; }
    }
}
