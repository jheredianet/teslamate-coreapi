using System.Text.Json.Serialization;

namespace coreAPI.Models
{
    public partial class ShortTimeDrive
    {
        public int PrevId { get; set; }
        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal DiffMinutes { get; set; }

    }
}
