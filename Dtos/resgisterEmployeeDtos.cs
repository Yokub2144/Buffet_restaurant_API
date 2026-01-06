using System.ComponentModel.DataAnnotations;
namespace Buffet_Restaurant_Managment_System_API.Dtos
{
    public class resgisterEmployeeDtos
    {
        [Required]
        public string Fullname { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public string Phone { get; set; }
        [Required]
        public string Password { get; set; }
        [Required]
        public string Gender { get; set; }
        [Required]
        public string Identification_Number { get; set; }
        [Required]
        public string Address { get; set; }
        public IFormFile? Image_Profile { get; set; }
        [Required]
        public string Department { get; set; }
        [Required]
        public string Employee_Type { get; set; } 
    
    }
}