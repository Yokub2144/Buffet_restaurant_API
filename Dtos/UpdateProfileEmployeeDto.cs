using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_Managment_System_API.Dtos
{
    public class editEmployeeProfileDtos
    {
        public int Emp_id { get; set; }
        public string? Fullname { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Password { get; set; }
        public string? Address { get; set; }
        public IFormFile? Image_Profile { get; set; }
    }
}