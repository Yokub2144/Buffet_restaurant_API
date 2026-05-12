using System.ComponentModel.DataAnnotations;

namespace BUFFET_RESTAURANT_API.Models
{
    public class ResImage
    {
        [Key]
        public int Image_id { get; set; }
        public string Image_Url { get; set; } = string.Empty;
        public string Image_Type { get; set; } = string.Empty;
    }
}