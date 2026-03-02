using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_Managment_System_API.Dtos
{
   public class updataDepartmentEmp
    {
        [Required]
        public int Emp_id {get; set;} 
        [Required]
        public string Department {get;set ;}
    }
}