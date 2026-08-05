using Buffet_Restaurant_API.Dtos;
using Buffet_Restaurant_API.Models;
using Buffet_Restaurant_Managment_System_API.Data;
using Buffet_Restaurant_Managment_System_API.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using QRCoder;
using SkiaSharp;
using System.Net.Sockets;

namespace Buffet_Restaurant_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrderController : ControllerBase
    {
        private readonly restaurantDbContext _context;
        private readonly IHubContext<tableStatusHub> _hubContext;

        public OrderController(restaurantDbContext context, IHubContext<tableStatusHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        // 🟢 1. Checkout สั่งซื้ออาหาร
        [HttpPost("checkout")]
        public async Task<IActionResult> PlaceOrder([FromBody] OrderDto dto)
        {
            var cart = await _context.Carts.FindAsync(dto.CartId);
            if (cart == null)
            {
                return NotFound(new { message = "ไม่พบตะกร้าสินค้าที่ระบุ" });
            }

            var cartItems = await _context.CartItems
                .Where(ci => ci.Cart_id == dto.CartId)
                .ToListAsync();

            if (!cartItems.Any())
            {
                return BadRequest(new { message = "ไม่มีรายการสินค้าในตะกร้า" });
            }

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
                var newOrder = new Orders
                {
                    Bill_id = dto.BillId,
                    Order_type = orderTypeDisplay,
                    OrderDateTime = DateTime.Now,
                    Order_Status = initialStatus
                };

                _context.Orders.Add(newOrder);
                await _context.SaveChangesAsync();

                var menuIds = cartItems.Select(ci => ci.Menu_id).ToList();
                var menus = await _context.Menus
                    .Where(m => menuIds.Contains(m.Menu_id))
                    .ToDictionaryAsync(m => m.Menu_id, m => m);

                var orderDetails = new List<Order_detail>();

                foreach (var item in cartItems)
                {
                    decimal currentPrice = menus.TryGetValue(item.Menu_id, out var menuObj) ? (menuObj.Price ?? 0m) : 0m;

                    orderDetails.Add(new Order_detail
                    {
                        Order_id = newOrder.Order_id,
                        menu_id = item.Menu_id,
                        Quantity = item.Quantity,
                        PriceAtOrderTime = currentPrice
                    });
                }

                _context.Order_detail.AddRange(orderDetails);
                await _context.SaveChangesAsync();

                _context.CartItems.RemoveRange(cartItems);
                await _context.SaveChangesAsync();

                _context.Carts.Remove(cart);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // 🔔 สะกิด SignalR แจ้งเตือนครัว — ยิงเป็น Order_id (แยกใบต่อรอบสั่ง)
                await _hubContext.Clients.All.SendAsync("NewKitchenOrder", newOrder.Order_id);

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
                var detailedError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "เกิดข้อผิดพลาดในการสั่งอาหาร", error = detailedError });
            }
        }

        // 🍳 2. ดึงสลิปใบบิลครัว (ต่อ 1 ออเดอร์) + เจน QR Code ส่งขึ้น Cloudinary + พิมพ์ออก Simulator 9100
        [HttpGet("getKitchenTicket/{orderId}")]
        public async Task<IActionResult> GetKitchenTicket(int orderId, [FromServices] Cloudinary cloudinary = null)
        {
            var order = await _context.Orders
                .Where(o => o.Order_id == orderId)
                .Select(o => new
                {
                    OrderId = o.Order_id,
                    OrderTime = o.OrderDateTime,
                    OrderStatus = o.Order_Status,
                    BillId = o.Bill_id
                })
                .FirstOrDefaultAsync();

            if (order == null) return NotFound(new { message = "ไม่พบรายการออเดอร์" });

            var tableNumbers = await (from gt in _context.GroupTables
                                      join t in _context.Tables on gt.Table_id equals t.Table_id
                                      where gt.Bill_id == order.BillId
                                      select t.Table_Number).ToListAsync();

            string tableDisplay = tableNumbers.Any() ? string.Join(", ", tableNumbers) : "ไม่ระบุโต๊ะ";

            var items = await (from od in _context.Order_detail
                               join m in _context.Menus on od.menu_id equals m.Menu_id
                               where od.Order_id == orderId
                               select new
                               {
                                   MenuId = od.menu_id,
                                   MenuName = m.Menu_Name,
                                   Quantity = od.Quantity
                               }).ToListAsync();

            // 📲 สร้าง QR Code Cloudinary URL (สำหรับแสดงบนหน้าเว็บ)
            string serveUrl = $"https://buffet-restaurant-management-system.vercel.app/serve-action?orderId={orderId}";
            string qrCodeCloudinaryUrl = "";

            if (cloudinary != null)
            {
                try
                {
                    using (QRCodeGenerator qrGenerator = new QRCodeGenerator())
                    {
                        QRCodeData qrCodeData = qrGenerator.CreateQrCode(serveUrl, QRCodeGenerator.ECCLevel.Q);
                        PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
                        byte[] qrCodeBytes = qrCode.GetGraphic(20);

                        using (var stream = new MemoryStream(qrCodeBytes))
                        {
                            var uploadParams = new ImageUploadParams()
                            {
                                File = new FileDescription($"qr_serve_order_{orderId}.png", stream),
                                Folder = "restaurant_serve_qrcodes",
                                PublicId = $"order_serve_{orderId}_{Guid.NewGuid()}"
                            };

                            var uploadResult = await cloudinary.UploadAsync(uploadParams);
                            if (uploadResult.Error == null)
                            {
                                qrCodeCloudinaryUrl = uploadResult.SecureUrl.ToString();
                            }
                        }
                    }
                }
                catch { }
            }

            // 🖨️ พิมพ์ตั๋วครัวเป็นรูปภาพ (ฟอนต์ Tahoma เดียวกับใบเสร็จหลังบ้าน) + QR ฝังในภาพเดียวกัน
            var printItems = items.Select(i => (i.MenuName, i.Quantity)).ToList();
            _ = EscPosPrinterHelper.PrintKitchenTicket(order.OrderId, tableDisplay, order.OrderTime, printItems);

            return Ok(new
            {
                OrderId = order.OrderId,
                TableNumber = tableDisplay,
                OrderTime = order.OrderTime,
                OrderStatus = order.OrderStatus,
                ServeQrCode = qrCodeCloudinaryUrl,
                Items = items
            });
        }

        // 📲 3. Endpoint สแกนนำเสิร์ฟ (ต่อ 1 ออเดอร์)
        [HttpGet("{orderId}/serve")]
        [HttpPost("{orderId}/serve")]
        public async Task<IActionResult> ServeOrder(int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Order_id == orderId);
            if (order == null) return NotFound(new { message = "ไม่พบรายการออเดอร์" });

            order.Order_Status = "SERVED";
            await _context.SaveChangesAsync();

            // 🔔 แจ้งครัวให้เอาการ์ดออกจากจอทันที
            await _hubContext.Clients.All.SendAsync("OrderStatusUpdated", new { orderId = orderId, status = "SERVED" });

            return Ok(new { message = "นำเสิร์ฟเรียบร้อยแล้ว", orderId = orderId, status = order.Order_Status });
        }

        // 🔍 4. ดึงรายละเอียดออเดอร์
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
        [HttpGet("getServeInfo/{orderId}")]
        public async Task<IActionResult> GetServeInfo(int orderId)
        {
            var order = await _context.Orders
                .Where(o => o.Order_id == orderId)
                .Select(o => new { OrderId = o.Order_id, OrderStatus = o.Order_Status, BillId = o.Bill_id })
                .FirstOrDefaultAsync();

            if (order == null) return NotFound(new { message = "ไม่พบรายการออเดอร์" });

            var tableNumbers = await (from gt in _context.GroupTables
                                      join t in _context.Tables on gt.Table_id equals t.Table_id
                                      where gt.Bill_id == order.BillId
                                      select t.Table_Number).ToListAsync();
            string tableDisplay = tableNumbers.Any() ? string.Join(", ", tableNumbers) : "ไม่ระบุโต๊ะ";

            var items = await (from od in _context.Order_detail
                               join m in _context.Menus on od.menu_id equals m.Menu_id
                               where od.Order_id == orderId
                               select new { MenuName = m.Menu_Name, Quantity = od.Quantity }).ToListAsync();

            return Ok(new
            {
                OrderId = order.OrderId,
                TableNumber = tableDisplay,
                OrderStatus = order.OrderStatus,
                Items = items
            });
        }
        // 💳 5. ดึงรายการชำระเงินตาม BillId
        [HttpGet("getBillPricedItems/{billId}")]
        public async Task<IActionResult> GetBillPricedItems(int billId)
        {
            var pricedItems = await (from od in _context.Order_detail
                                     join o in _context.Orders on od.Order_id equals o.Order_id
                                     join m in _context.Menus on od.menu_id equals m.Menu_id
                                     where o.Bill_id == billId
                                           && o.Order_Status == "ดำเนินการเสร็จสิ้น"
                                           && od.PriceAtOrderTime > 0
                                     select new
                                     {
                                         od.Orderdetail_id,
                                         od.Order_id,
                                         Menu_id = od.menu_id,
                                         MenuName = m.Menu_Name,
                                         od.Quantity,
                                         od.PriceAtOrderTime,
                                         SubTotal = od.Quantity * od.PriceAtOrderTime
                                     }).ToListAsync();

            if (!pricedItems.Any())
            {
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
                totalPrice = pricedItems.Sum(i => i.SubTotal),
                items = pricedItems
            });
        }
    }

    // 🖨️ Helper Class: พิมพ์ตั๋วครัวเป็น "รูปภาพ" (ฟอนต์ Tahoma เดียวกับใบเสร็จหลังบ้าน PrintController.cs)
    // แทนที่การพิมพ์ข้อความ ESC/POS ดิบแบบเดิม เพื่อให้หน้าตาตรงกับใบเสร็จหลังบ้าน 100%
    public static class EscPosPrinterHelper
    {
        private static readonly string _printerIp = "127.0.0.1";
        private static readonly int _printerPort = 9100;

        // 🖨️ พิมพ์ตั๋วครัว 1 ใบต่อ 1 ออเดอร์ แบบรูปภาพ: หัวร้าน / เลขที่ใบเสร็จ / วันที่ (พ.ศ.) / โต๊ะ / รายการอาหาร / QR
        public static async Task PrintKitchenTicket(int orderId, string tableNumber, DateTime orderTime, List<(string Name, int Qty)> items)
        {
            using SKBitmap bitmap = DrawTicketImage(orderId, tableNumber, orderTime, items);
            byte[] imageBytes = ConvertBitmapToEscPosRaster(bitmap);

            try
            {
                using var client = new TcpClient();
                await client.ConnectAsync(_printerIp, _printerPort);
                using var stream = client.GetStream();

                var bytes = new List<byte>();
                bytes.AddRange(new byte[] { 0x1B, 0x40 });               // Reset
                bytes.AddRange(imageBytes);                              // ภาพใบตั๋ว (รวม QR ในตัว)
                bytes.AddRange(new byte[] { 0x1B, 0x64, 0x03 });         // Feed
                bytes.AddRange(new byte[] { 0x1D, 0x56, 0x42, 0x00 });   // Cut

                byte[] data = bytes.ToArray();
                await stream.WriteAsync(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"พิมพ์ตั๋วครัวไม่สำเร็จ: {ex.Message}");
            }
        }

        // 🎨 วาดตั๋วครัวลง Bitmap ด้วยฟอนต์ Tahoma เดียวกับ PrintController.cs
        private static SKBitmap DrawTicketImage(int orderId, string tableNumber, DateTime orderTime, List<(string Name, int Qty)> items)
        {
            int width = 576;
            int estimatedHeight = 700 + items.Count * 40;

            SKBitmap bitmap = new SKBitmap(width, estimatedHeight);
            using SKCanvas canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.White);

            SKTypeface typeface = SKTypeface.FromFamilyName("Tahoma", SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                                 ?? SKTypeface.Default;
            SKTypeface boldTypeface = SKTypeface.FromFamilyName("Tahoma", SKFontStyleWeight.Bold, SKFontStyleWidth.Normal, SKFontStyleSlant.Upright)
                                 ?? SKTypeface.Default;

            using SKFont fontNormal = new SKFont(typeface, 22);
            using SKFont fontHeader = new SKFont(boldTypeface, 36);

            using SKPaint paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

            float y = 50;
            float leftMargin = 15;
            float rightMargin = width - 15;

            void DrawTextLeft(string text, float targetY, SKFont font) => canvas.DrawText(text, leftMargin, targetY, SKTextAlign.Left, font, paint);
            void DrawTextRight(string text, float targetY, SKFont font) => canvas.DrawText(text, rightMargin, targetY, SKTextAlign.Right, font, paint);
            void DrawTextCenter(string text, float targetY, SKFont font) => canvas.DrawText(text, width / 2f, targetY, SKTextAlign.Center, font, paint);

            void DrawRow(string left, string right, SKFont? font = null, float lineSpacing = 38)
            {
                var f = font ?? fontNormal;
                DrawTextLeft(left, y, f);
                DrawTextRight(right, y, f);
                y += lineSpacing;
            }

            void DrawDivider()
            {
                using var linePaint = new SKPaint
                {
                    Color = SKColors.Gray,
                    StrokeWidth = 2,
                    PathEffect = SKPathEffect.CreateDash(new float[] { 8, 4 }, 0)
                };
                canvas.DrawLine(leftMargin, y - 10, rightMargin, y - 10, linePaint);
                y += 15;
            }

            // --- HEADER ---
            DrawTextCenter("ร้าน BUFFET", y, fontHeader);
            y += 50;
            DrawDivider();

            // --- INFO --- (แปลงปีเป็น พ.ศ. ให้ตรงกับใบเสร็จจริง)
            string thaiDateStr = $"{orderTime:dd/MM}/{orderTime.Year + 543} {orderTime:HH:mm:ss}";
            DrawRow("เลขที่ใบเสร็จ:", $"{orderId:D5}");
            DrawRow("วันที่:", thaiDateStr);
            DrawRow("โต๊ะ:", tableNumber);
            DrawDivider();
            y += 10;

            // --- รายการอาหาร ---
            foreach (var item in items)
                DrawRow(item.Name + ":", item.Qty.ToString());

            DrawDivider();
            y += 20;

            // --- QR (ฝังเป็นรูปในภาพเดียวกันเลย ไม่ต้องยิงคำสั่ง raster แยก) ---
            string serveUrl = $"https://buffet-restaurant-management-system.vercel.app/serve-action?orderId={orderId}";
            using (var qrGen = new QRCodeGenerator())
            {
                var qrData = qrGen.CreateQrCode(serveUrl, QRCodeGenerator.ECCLevel.Q);
                var qrPng = new PngByteQRCode(qrData);
                byte[] qrBytes = qrPng.GetGraphic(6);

                using SKBitmap qrBitmap = SKBitmap.Decode(qrBytes);
                int qrSize = 260;
                float qrX = (width - qrSize) / 2f;
                var srcRect = new SKRect(0, 0, qrBitmap.Width, qrBitmap.Height);
                var destRect = new SKRect(qrX, y, qrX + qrSize, y + qrSize);
                canvas.DrawBitmap(qrBitmap, srcRect, destRect, SKSamplingOptions.Default, paint);
                y += qrSize + 20;
            }

            int finalHeight = (int)y;
            SKBitmap cropped = new SKBitmap(width, finalHeight);
            using (SKCanvas cropCanvas = new SKCanvas(cropped))
            {
                var rect = new SKRect(0, 0, width, finalHeight);
                cropCanvas.DrawBitmap(bitmap, rect, rect, SKSamplingOptions.Default, paint);
            }
            return cropped;
        }

        // 🖼️ แปลง Bitmap เป็น ESC/POS raster bit image (GS v 0) แบบเดียวกับ PrintController.cs
        private static byte[] ConvertBitmapToEscPosRaster(SKBitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            int widthBytes = (width + 7) / 8;

            List<byte> bytes = new List<byte>();
            bytes.AddRange(new byte[] { 0x1D, 0x76, 0x30, 0x00 });
            bytes.Add((byte)(widthBytes % 256));
            bytes.Add((byte)(widthBytes / 256));
            bytes.Add((byte)(height % 256));
            bytes.Add((byte)(height / 256));

            for (int y = 0; y < height; y++)
            {
                for (int xByte = 0; xByte < widthBytes; xByte++)
                {
                    byte b = 0;
                    for (int bit = 0; bit < 8; bit++)
                    {
                        int x = (xByte * 8) + bit;
                        if (x < width)
                        {
                            SKColor color = bitmap.GetPixel(x, y);
                            int luminance = (int)(color.Red * 0.3 + color.Green * 0.59 + color.Blue * 0.11);
                            if (luminance < 128)
                            {
                                b |= (byte)(0x80 >> bit);
                            }
                        }
                    }
                    bytes.Add(b);
                }
            }

            return bytes.ToArray();
        }

    }
}