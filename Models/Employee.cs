using System.ComponentModel.DataAnnotations;
namespace Buffet_Restaurant_Managment_System_API.Models
{
    public class Employee
    {
        [Key]
        public int Emp_id { get; set; }
        public string Fullname { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Password { get; set; } = null!;
        public string Gender { get; set; } = null!;
        public string Identification_Number { get; set; } = null!;
        public string Address { get; set; } = null!;
        public string Image_Profile { get; set; } = null!;
        public string Department { get; set; } = null!; 
        public decimal? Wage { get; set; }
        public string? Employee_Type { get; set; } 
        public string? Employee_Status { get; set; }
    }
}