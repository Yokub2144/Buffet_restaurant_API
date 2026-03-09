using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_Managment_System_API.Dtos
{
    public class updateTables
    {
        [Required]
        public int Table_id { get; set; }
        [Required]
        public string? Table_Number { get; set; }
    }
}