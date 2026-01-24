using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_API.Dtos
{
    public class MenuDto
    {
        [Required]
        public string Menu_Name { get; set; }

        public decimal? Price { get; set; }

        [Required]
        public string Category { get; set; }

        [Required]
        public string Menu_Image { get; set; }

        [Required]
        // ส่งค่า "อยู่ในบุฟเฟต์" หรือ "ไม่อยู่ในบุฟเฟต์"
        public string Menu_Type { get; set; }
    }
}