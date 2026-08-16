using System.ComponentModel.DataAnnotations;

namespace coreAPI.Models
{
    public class M3UEntry
    {
        public int Id { get; set; }

        [Required, StringLength(128)]
        public string GroupTitle { get; set; } = "";

        [Url, StringLength(512)]
        public string? TVGLogo { get; set; }

        [Required, StringLength(256)]
        public string ChannelName { get; set; } = "";

        [Required, StringLength(1024)]
        public string StreamUrl { get; set; } = "";

        [StringLength(128)]
        public string? TVGId { get; set; }


        // Mantener orden explícito en el fichero
        public int Order { get; set; }
    }
}
