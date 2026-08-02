using Buffet_Restaurant_API.Dtos;
using Buffet_Restaurant_API.Models;
using Buffet_Restaurant_Managment_System_API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Buffet_Restaurant_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly restaurantDbContext _context;
        public OrderController(restaurantDbContext context)
        {
            _context = context;
        }
        [HttpPost("checkout")]
        public async Task<IActionResult> PlaceOrder([FromBody] OrderDto dto)
        {
            // 1. ตรวจสอบ Cart
            var cart = await _context.Carts.FindAsync(dto.CartId);
            if (cart == null)
            {
                return NotFound(new { message = "ไม่พบตะกร้าสินค้าที่ระบุ" });
            }

            // 2. ดึงรายการใน Cart_item
            var cartItems = await _context.CartItems
                .Where(ci => ci.Cart_id == dto.CartId)
                .ToListAsync();

            if (!cartItems.Any())
            {
                return BadRequest(new { message = "ไม่มีรายการสินค้าในตะกร้า" });
            }

            // 3. จัดการประเภท Order และกำหนดสถานะเริ่มต้น
            string orderTypeDisplay;
            string initialStatus;

            if (dto.OrderType?.ToLower() == "preorder" || dto.OrderType == "สั่งล่วงหน้า")
            {
                orderTypeDisplay = "สั่งล่วงหน้า";
                initialStatus = "รับออเดอร์";
            }
            else
            {
                orderTypeDisplay = "สั่งหน้าร้าน";
                initialStatus = "กำลังจัดเตรียมอาหาร";
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 4. บันทึกข้อมูลลงตาราง Orders
                var newOrder = new Orders
                {
                    Bill_id = dto.BillId,
                    Order_type = orderTypeDisplay,
                    OrderDateTime = DateTime.Now,
                    Order_Status = initialStatus
                };

                _context.Orders.Add(newOrder);
                await _context.SaveChangesAsync(); // บันทึกเพื่อเอา newOrder.Order_id มาใช้ต่อ

                // 5. ดึงข้อมูล Menu ทั้งหมดทีเดียว + แปลง Price ?? 0 เพื่อแก้ Type Mismatch (decimal?)
                var menuIds = cartItems.Select(ci => ci.Menu_id).ToList();
                var menus = await _context.Menus
                    .Where(m => menuIds.Contains(m.Menu_id))
                    .ToDictionaryAsync(m => m.Menu_id, m => m.Price ?? 0m);

                // บันทึกข้อมูลลงตาราง Order_detail
                var orderDetails = new List<Order_detail>();

                foreach (var item in cartItems)
                {
                    decimal currentPrice = menus.TryGetValue(item.Menu_id, out decimal price) ? price : 0m;

                    orderDetails.Add(new Order_detail
                    {
                        Order_id = newOrder.Order_id,
                        menu_id = item.Menu_id,
                        Quantity = item.Quantity,
                        PriceAtOrderTime = currentPrice
                    });
                }

                _context.Order_detail.AddRange(orderDetails);
                await _context.SaveChangesAsync(); // บันทึก Order_detail ให้เสร็จเรียบร้อย

                // 6. ลบข้อมูล Cart_item (ลบลูกออกก่อนเพื่อไม่ให้ติด Foreign Key Constraint)
                _context.CartItems.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                // 7. ลบข้อมูล Cart (ลบแม่)
                _context.Carts.Remove(cart);
                await _context.SaveChangesAsync();

                // ยืนยันการทำ Transaction ทั้งหมด
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = $"สั่งอาหาร ({orderTypeDisplay}) เรียบร้อยแล้ว",
                    Order_id = newOrder.Order_id,
                    Order_type = orderTypeDisplay,
                    Order_Status = initialStatus
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                // ดึง InnerException ออกมาดูรายละเอียดหากมี Error ฝั่ง Database
                var detailedError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการสั่งอาหาร", error = detailedError });
            }
        }
        [HttpGet("getOrderDetails/{orderId}")]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            var orderDetails = await _context.Order_detail
                .Where(od => od.Order_id == orderId)
                .Select(od => new
                {
                    Menu_id = od.menu_id,
                    Quantity = od.Quantity,
                    PriceAtOrderTime = od.PriceAtOrderTime
                })
                .ToListAsync();

            if (orderDetails == null || !orderDetails.Any())
            {
                return NotFound(new { message = "ไม่พบข้อมูลคำสั่งซื้อ" });
            }

            return Ok(orderDetails);
        }
        [HttpGet("getBillPricedItems/{billId}")]
        public async Task<IActionResult> GetBillPricedItems(int billId)
        {
            // ดึงรายการอาหารที่มีราคามากกว่า 0 จากทุก Order ที่สังกัดใน billId นี้
           var pricedItems = await (from od in _context.Order_detail
                             join o in _context.Orders on od.Order_id equals o.Order_id
                             where o.Bill_id == billId 
                                   && o.Order_Status == "ดำเนินการเสร็จสิ้น" 
                                   && od.PriceAtOrderTime > 0
                             select new
                             {
                                 od.Orderdetail_id,
                                 od.Order_id,
                                 od.menu_id,
                                 MenuName = od.Menu != null ? od.Menu.Menu_Name : null,
                                 od.Quantity,
                                 od.PriceAtOrderTime,
                                 SubTotal = od.Quantity * od.PriceAtOrderTime
                             }).ToListAsync();

            if (!pricedItems.Any())
            {
                // กรณีบิลนี้สั่งแต่รายการบุฟเฟต์ล้วน ไม่มีรายการจ่ายเงินเพิ่มเลย
                return Ok(new
                {
                    billId = billId,
                    message = "ไม่มีรายการอาหารที่ต้องชำระเงินเพิ่มในบิลนี้",
                    totalPrice = 0,
                    items = new List<object>()
                });
            }

            return Ok(new
            {
                billId = billId,
                totalPrice = pricedItems.Sum(i => i.SubTotal), // ยอดรวมเฉพาะเมนูชำระเงินเพิ่มทั้งบิล
                items = pricedItems
            });
        }
    }
}