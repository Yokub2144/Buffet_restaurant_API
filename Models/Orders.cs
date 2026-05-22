namespace Buffet_Restaurant_API.Models
{
    public class Orders
    {
        public int Order_id {get; set;}
        public int Bill_id {get; set;}
        public string? Order_type {get; set;}
        public DateTime OrderDateTime {get; set;}
        public string? Order_Status {get; set;}
    }
}