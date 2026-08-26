using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Buffet_Restaurant_API.Models
{
    public class Orders
    {
        [Key]
        public int Order_id { get; set; }
        public int? Bill_id { get; set; }

        public string? Order_type { get; set; }
        public DateTime OrderDateTime { get; set; }
        public string? Order_Status { get; set; }

        [ForeignKey("Bill_id")]
        public virtual Bill? Bill { get; set; }


    }
}