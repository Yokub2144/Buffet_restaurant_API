using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Buffet_Restaurant_API.Models
{
    public class Order_detail
    {
        [Key]
        public int Orderdetail_id {get; set;}
        public int Order_id {get; set;}
        public int menu_id {get; set;}
        public int Quantity {get; set;}
        public decimal PriceAtOrderTime {get; set;}

        [ForeignKey("menu_id")]
        public virtual Menu Menu { get; set; }

        [ForeignKey("Order_id")]
        public virtual Orders Orders { get; set; }

    }
}