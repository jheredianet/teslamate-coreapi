namespace coreAPI.Models
{
    public partial class Token
    {
        public int Id { get; set; }
        public DateTime InsertedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[]? Refresh { get; set; }
        public byte[]? Access { get; set; }
    }
}
