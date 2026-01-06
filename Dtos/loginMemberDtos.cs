using System.ComponentModel.DataAnnotations;
namespace Buffet_Restaurant_Managment_System_API.Dtos
{
    public class loginMemberDtos
    {
        [Required]
        public string Phone { get; set; }
        [Required]
        public string Password { get; set; }
    }
}