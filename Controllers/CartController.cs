using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Buffet_Restaurant_API.Models;
using Buffet_Restaurant_API.Dtos;
using Buffet_Restaurant_Managment_System_API.Data;

namespace Buffet_Restaurant_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly restaurantDbContext _context;

        public CartController(restaurantDbContext context)
        {
            _context = context;
        }

        // 1. เพิ่ม/ลด รายการ 
        [HttpPost("add-item")]
        public async Task<IActionResult> AddItemToCart([FromBody] AddToCartDtos request)
        {

            var cart = await _context.Carts
                                     .Where(c => c.Table_id == request.TableId)
                                     .OrderByDescending(c => c.Created_at)
                                     .FirstOrDefaultAsync();

            if (cart == null)
            {
                cart = new Cart
                {
                    Table_id = request.TableId,
                    Booking_id = null,
                    Created_at = DateTime.Now
                };
                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            //จัดการรายการอาหาร
            var cartItem = await _context.CartItems
                                         .FirstOrDefaultAsync(ci => ci.Cart_id == cart.Cart_id && ci.Menu_id == request.MenuId);

            if (cartItem != null)
            {
                cartItem.Quantity += request.Quantity;

                if (cartItem.Quantity <= 0)
                {
                    _context.CartItems.Remove(cartItem);
                }
                else
                {
                    _context.CartItems.Update(cartItem);
                }
            }
            else
            {
                if (request.Quantity > 0)
                {
                    cartItem = new Cart_item
                    {
                        Cart_id = cart.Cart_id,
                        Menu_id = request.MenuId,
                        Quantity = request.Quantity
                    };
                    await _context.CartItems.AddAsync(cartItem);
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "อัปเดตรายการสำเร็จ" });
        }

        //  ลบรายการ 
        [HttpDelete("delete-item/{cartItemId}")]
        public async Task<IActionResult> DeleteItem(int cartItemId)
        {
            var item = await _context.CartItems.FindAsync(cartItemId);
            if (item == null) return NotFound(new { message = "ไม่พบรายการ" });

            _context.CartItems.Remove(item);
            await _context.SaveChangesAsync();

            return Ok(new { message = "ลบรายการเรียบร้อย" });
        }

        //  ดึงตะกร้า 
        [HttpGet("get-items/{tableId}")]
        public async Task<IActionResult> GetCartItems(int tableId)
        {
            var cart = await _context.Carts
                                     .Where(c => c.Table_id == tableId)
                                     .OrderByDescending(c => c.Created_at)
                                     .FirstOrDefaultAsync();

            if (cart == null) return Ok(new { items = new List<object>() });

            var items = await _context.CartItems
                                      .Where(ci => ci.Cart_id == cart.Cart_id)
                                      .Join(_context.Menus,
                                            ci => ci.Menu_id,
                                            m => m.Menu_id,
                                            (ci, m) => new
                                            {
                                                id = ci.Cartitem_id,
                                                menuId = m.Menu_id,
                                                name = m.Menu_Name,
                                                price = m.Price ?? 0,
                                                quantity = ci.Quantity,
                                                image = m.Menu_Image,
                                                selected = true
                                            })
                                      .ToListAsync();

            return Ok(new { cartId = cart.Cart_id, items = items });
        }

    }
}