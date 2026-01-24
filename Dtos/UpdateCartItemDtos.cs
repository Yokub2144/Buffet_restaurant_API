using System.ComponentModel.DataAnnotations;

namespace Buffet_Restaurant_API.Dtos
{
    public class UpdateCartItemDto
    {
        [Required]
        public int CartItemId { get; set; }

        [Required]
        public int NewQuantity { get; set; }
    }
}