using System.ComponentModel.DataAnnotations;

namespace coreAPI.Models
{
    public class ServerMapping
    {
        public int Id { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Identificador")]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(512)]
        [Display(Name = "URL base")]
        public string BaseUrl { get; set; } = string.Empty;
    }
}
