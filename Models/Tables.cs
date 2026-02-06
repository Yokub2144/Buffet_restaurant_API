using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_Managment_System_API.Models
{
    public class Tables
    {
        [Key]
        public int Table_id { get; set; }
        public string Table_Number { get; set; } = null!;
        public string Table_Status { get; set; } = null!;
        public string Table_QR_Code { get; set; } = null!;
    }
}