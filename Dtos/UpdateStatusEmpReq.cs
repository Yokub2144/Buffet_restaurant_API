using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_Managment_System_API.Dtos
{
    public class updateStatusEmpReq
    {
        [Required]
        public int Emp_id {get;set;}
        [Required]
        public string Employee_Status {get; set;}

    }
}