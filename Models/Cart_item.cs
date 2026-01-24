using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Buffet_Restaurant_API.Models
{
    [Table("Cart_item")]
    public class Cart_item
    {
        [Key]
        [Column("Cartitem_id")]
        public int Cartitem_id { get; set; }

        [Column("Cart_id")]
        public int Cart_id { get; set; }

        [Column("Menu_id")]
        public int Menu_id { get; set; }

        [Column("Quantity")]
        public int Quantity { get; set; }
    }
}