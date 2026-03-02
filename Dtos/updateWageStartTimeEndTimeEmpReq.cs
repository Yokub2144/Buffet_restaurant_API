namespace Buffet_Restaurant_Managment_System_API.Dtos
{
    public class updateWageStartTimeEndTimeEmpReq
    {
        public int Emp_id {get; set;}
        public decimal? Wage {get; set;}
        public TimeSpan? Start_Time {get; set;}
        public TimeSpan? End_Time {get;set;}
    }
}