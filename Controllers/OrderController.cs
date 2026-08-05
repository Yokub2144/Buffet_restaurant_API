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
using System.Net.Sockets;
using System.Text;

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

            // 🖨️ พิมพ์ออก Simulator ผ่าน Singleton Stream (พิมพ์ QR ลงกระดาษจริงด้วย)
            var printItems = items.Select(i => (i.MenuName, i.Quantity)).ToList();
            Task.Run(() => EscPosPrinterHelper.PrintKitchenTicket(order.OrderId, tableDisplay, order.OrderTime, printItems));

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

    // 🖨️ Helper Class แบบ Persistent Singleton Connection (แก้ภาษาต่างดาว + ตัดกระดาษแยกบิลไม่ให้ปิดเอง)
    public static class EscPosPrinterHelper
    {
        private static readonly string _printerIp = "127.0.0.1";
        private static readonly int _printerPort = 9100;
        private static TcpClient _client = null;
        private static NetworkStream _stream = null;
        private static readonly object _lockObj = new object();

        private static bool EnsureConnected()
        {
            lock (_lockObj)
            {
                try
                {
                    if (_client != null && _client.Connected && _stream != null)
                    {
                        return true;
                    }

                    _stream?.Dispose();
                    _client?.Close();

                    _client = new TcpClient();
                    var result = _client.BeginConnect(_printerIp, _printerPort, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));

                    if (success && _client.Connected)
                    {
                        _client.EndConnect(result);
                        _stream = _client.GetStream();
                        return true;
                    }
                }
                catch
                {
                    _client = null;
                    _stream = null;
                }
                return false;
            }
        }

        // 🖨️ พิมพ์ตั๋วครัว 1 ใบต่อ 1 ออเดอร์: หัวร้าน / เลขที่ใบเสร็จ / วันที่ / โต๊ะ / รายการอาหาร / QR
        public static void PrintKitchenTicket(int orderId, string tableNumber, DateTime orderTime, List<(string Name, int Qty)> items)
        {
            lock (_lockObj)
            {
                if (!EnsureConnected()) return;

                try
                {
                    // ใช้ UTF-8 เพื่อรองรับภาษาไทยใน ESC/POS Simulator v3
                    Encoding utf8 = Encoding.UTF8;
                    int lineWidth = 32; // กระดาษ 58mm ≈ 32 ตัวอักษร/บรรทัด

                    // 1. Reset Printer (ESC @)
                    _stream.Write(new byte[] { 0x1B, 0x40 }, 0, 2);

                    // 2. Set Code Page UTF-8
                    _stream.Write(new byte[] { 0x1C, 0x2E }, 0, 2);

                    // ---- หัวร้าน: กึ่งกลาง ----
                    _stream.Write(new byte[] { 0x1B, 0x61, 1 }, 0, 3); // Align center
                    WriteLine(utf8, "ร้าน BUFFET");
                    WriteLine(utf8, new string('-', lineWidth));

                    // ---- ข้อมูลบิล: label ซ้าย / value ขวา ----
                    _stream.Write(new byte[] { 0x1B, 0x61, 0 }, 0, 3); // Align left
                    WriteLine(utf8, PadTwoCol("เลขที่ใบเสร็จ:", $"B{orderId:D5}", lineWidth));
                    WriteLine(utf8, PadTwoCol("วันที่:", orderTime.ToString("dd/MM/yyyy HH:mm:ss"), lineWidth));
                    WriteLine(utf8, PadTwoCol("โต๊ะ:", tableNumber, lineWidth));
                    WriteLine(utf8, new string('-', lineWidth));

                    // ---- รายการอาหาร ----
                    foreach (var item in items)
                        WriteLine(utf8, PadTwoCol(item.Name, item.Qty.ToString(), lineWidth));

                    WriteLine(utf8, new string('-', lineWidth));

                    // ---- พิมพ์ QR จริงลงกระดาษ ----
                    _stream.Write(new byte[] { 0x1B, 0x61, 1 }, 0, 3); // Align center
                    using (var qrGen = new QRCodeGenerator())
                    {
                        var serveUrl = $"https://buffet-restaurant-management-system.vercel.app/serve-action?orderId={orderId}";
                        var qrData = qrGen.CreateQrCode(serveUrl, QRCodeGenerator.ECCLevel.Q);
                        PrintQrRaster(_stream, qrData, moduleSize: 4);
                    }

                    // 3. คำสั่งตัดกระดาษแบบ Partial Cut (GS V 66 0) แยกบิลชัดเจน
                    _stream.Write(new byte[] { 0x1D, 0x56, 66, 0 }, 0, 4);

                    // เคลียร์ Buffer ข้อมูลโดยไม่ปิดการเชื่อมต่อ
                    _stream.Flush();
                }
                catch
                {
                    _client?.Close();
                    _client = null;
                    _stream = null;
                }
            }

            void WriteLine(Encoding enc, string text)
            {
                var b = enc.GetBytes(text + "\n");
                _stream.Write(b, 0, b.Length);
            }
        }

        // จัด label ชิดซ้าย / value ชิดขวา ในความกว้างบรรทัดเท่ากับ width
        private static string PadTwoCol(string left, string right, int width)
        {
            int spaces = width - left.Length - right.Length;
            if (spaces < 1) spaces = 1;
            return left + new string(' ', spaces) + right;
        }

        // แปลง QRCodeData.ModuleMatrix (bool[,]) เป็น ESC/POS raster bit image (GS v 0) แล้วส่งตรงไปเครื่องพิมพ์
        private static void PrintQrRaster(NetworkStream stream, QRCodeData qrData, int moduleSize)
        {
            var matrix = qrData.ModuleMatrix;
            int modules = matrix.Count;
            int pixelSize = modules * moduleSize;
            int widthBytes = (pixelSize + 7) / 8;

            byte[] header = new byte[]
            {
                0x1D, 0x76, 0x30, 0x00,                 // GS v 0 m(=0 normal)
                (byte)(widthBytes & 0xFF), (byte)(widthBytes >> 8),
                (byte)(pixelSize & 0xFF), (byte)(pixelSize >> 8)
            };
            stream.Write(header, 0, header.Length);

            byte[] row = new byte[widthBytes];
            for (int y = 0; y < modules; y++)
            {
                for (int rep = 0; rep < moduleSize; rep++)
                {
                    Array.Clear(row, 0, row.Length);
                    for (int x = 0; x < modules; x++)
                    {
                        if (!matrix[y][x]) continue;
                        for (int rx = 0; rx < moduleSize; rx++)
                        {
                            int px = x * moduleSize + rx;
                            row[px / 8] |= (byte)(0x80 >> (px % 8));
                        }
                    }
                    stream.Write(row, 0, row.Length);
                }
            }
        }
    }
}