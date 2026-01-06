using System.ComponentModel.DataAnnotations;
namespace Buffet_Restaurant_Managment_System_API.Models
{
    public class Member
    {
        [Key]
        public int Member_id { get; set; }
        public string Fullname { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Password { get; set; } = null!;
        public DateOnly Birthday { get; set; }
    }
}