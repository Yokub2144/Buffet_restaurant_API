using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_Managment_System_API.Models
{
    public class Resconfig
    {
        [Key]
        public int Config_id { get; set; }
        public string Res_name { get; set; } = string.Empty;
        public string Res_phone { get; set; } = string.Empty;
        public decimal Price_Adult { get; set; }
        public decimal Price_Child { get; set; }
        public decimal Fine { get; set; }
    }
}