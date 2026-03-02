using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_API.Dtos
{
    public class MenuFormDto
    {
        public string Menu_Name { get; set; }
        public decimal? Price { get; set; }
        public string Category { get; set; }

        public string Menu_Type { get; set; }
        public IFormFile? ImageFile { get; set; }
    }
}