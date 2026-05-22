namespace Buffet_Restaurant_API.Models
{
    public class Ordere_detail
    {
        public int Orderdetail_id {get; set;}
        public int Order_id {get; set;}
        public int menu_id {get; set;}
        public int Quantity {get; set;}
        public decimal PriceAtOrderTime {get; set;}
    }
}