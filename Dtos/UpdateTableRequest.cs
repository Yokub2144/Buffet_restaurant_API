using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_Managment_System_API.Dtos
{
    public class updateTableStatus
    {
        [Required]
        public int tableId {get; set;}
        [Required]
        public string ?status {get; set;}
    }
}