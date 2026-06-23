namespace Buffet_Restaurant_Managment_System_API.Models
{
    public class DiscountDto
    {
        public string? Discount_Name { get; set; }
        public int Discount_amount { get; set; }
        public string? Discount_Type { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}