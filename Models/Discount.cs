using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Buffet_Restaurant_Managment_System_API.Models
{
    [Table("Discount")]
    public class Discount
    {
        [Key]
        public int Discount_id { get; set; }

        public string Discount_Name { get; set; } = string.Empty;

        public int Discount_amount { get; set; }

        public string Discount_Type { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }
    }
}