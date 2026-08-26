using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Buffet_Restaurant_API.Models
{
    public class Order_detail
    {
        [Key]
        [Column("Orderdetail_id")]
        public int Orderdetail_id { get; set; }

        [Column("Order_id")]
        public int Order_id { get; set; }

        [Column("Menu_id")]
        public int Menu_id { get; set; }

        [Column("Quantity")]
        public int Quantity { get; set; }

        [Column("PriceAtOrderTime")]
        public decimal PriceAtOrderTime { get; set; }

        [ForeignKey("Menu_id")]
        public virtual Menu? Menu { get; set; }

        [ForeignKey("Order_id")]
        public virtual Orders? Orders { get; set; }
    }
}