using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_Managment_System_API.Dtos
{
    public class updateTypeEmpReq
    {
        [Required]
        public int Emp_id {get; set;}
        [Required]
        public string Employee_Type {get; set;}
    }
}