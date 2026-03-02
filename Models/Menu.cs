using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Buffet_Restaurant_API.Models
{
    [Table("Menu")]
    public class Menu
    {
        [Key]
        [Column("Menu_id")]
        public int Menu_id { get; set; }

        [Column("Menu_Name")]
        public string Menu_Name { get; set; }

        [Column("Price")]
        public decimal? Price { get; set; }
        [Column("Category")]
        public string Category { get; set; }

        [Column("Menu_Image")]
        public string Menu_Image { get; set; }


        [Column("Menu_Type")]
        public string Menu_Type { get; set; }
    }
}