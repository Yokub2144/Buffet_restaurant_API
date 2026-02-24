using Microsoft.AspNetCore.Mvc;
using Buffet_Restaurant_Managment_System_API.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Sockets;
using System.Text;
using SkiaSharp;
using ESCPOS_NET;
using ESCPOS_NET.Utilities;
using ESCPOS_NET.Emitters;
using ESCPOS_NET.Printers;
namespace Buffet_Restaurant_Managment_System_API.Controllers
{
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly restaurantDbContext _context;

    public TestController(restaurantDbContext context)
    {
        _context = context;
    }

    [HttpGet("check-db")]
    public async Task<IActionResult> CheckConnection()
    {
        try
        {
            var employee = await _context.Employee.FirstOrDefaultAsync();
            return Ok(new { Message = "เชื่อมต่อสำเร็จ!", Data = employee });
        }
        catch (Exception ex)
        {
            return BadRequest(new { Message = "เชื่อมต่อล้มเหลว", Error = ex.Message });
        }
    }
    [HttpGet("print-test")]
    public async Task TestPrintLocal()
{
    string ipAddress = "127.0.0.1"; // รันในเครื่องตัวเอง
    int port = 9100;

    try
    {
        using (TcpClient client = new TcpClient(ipAddress, port))
        using (NetworkStream stream = client.GetStream())
        {
            // 1. เตรียมข้อความภาษาไทย (Windows-874)
            // หมายเหตุ: ต้องทำการ Register Encoding ก่อนใน .NET Core
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Encoding thaiEncoding = Encoding.GetEncoding(874);

            string receiptText = "===== TEST RECEIPT =====\n" +
                                 "Order: #A001\n" +
                                 "Menu: ข้าวกะเพราไก่\n" +
                                 "Price: 50 THB\n" +
                                 "========================\n\n\n";

            byte[] data = thaiEncoding.GetBytes(receiptText);

            // 2. คำสั่งตัดกระดาษ (ESC/POS: GS V 66 0)
            byte[] cutCommand = new byte[] { 0x1D, 0x56, 0x42, 0x00 };

            // 3. ส่งข้อมูลไปที่ Hercules (Simulator)
            await stream.WriteAsync(data, 0, data.Length);
            await stream.WriteAsync(cutCommand, 0, cutCommand.Length);

            Console.WriteLine("ส่งข้อมูลสำเร็จ! เช็คที่โปรแกรม Hercules");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"เชื่อมต่อไม่ได้: {ex.Message}");
    }
}
[HttpGet("print-image-test")]
public async Task<IActionResult> PrintImageTest()
{
    // --- 1. วาดรูปด้วย SkiaSharp (แก้เรื่อง TextSize obsolete) ---
    int width = 576; 
    int height = 300; 
    using var surface = SKSurface.Create(new SKImageInfo(width, height));
    var canvas = surface.Canvas;
    canvas.Clear(SKColors.White);

    var paint = new SKPaint { Color = SKColors.Black, IsAntialias = false, FilterQuality = SKFilterQuality.None };
    // ใช้ SKFont แทนการกำหนดผ่าน paint.TextSize เพื่อแก้ Warning
    var font = new SKFont(SKTypeface.FromFamilyName("Tahoma"), 30);

    canvas.DrawText("ใบเสร็จรับเงิน", 110, 50, font, paint);
    canvas.DrawText("--------------------------", 10, 90, font, paint);
    canvas.DrawText("กะเพราไก่ไข่ดาว    65.-", 10, 140, font, paint);
    canvas.DrawText("น้ำเปล่า            10.-", 10, 190, font, paint);
    canvas.DrawText("--------------------------", 10, 240, font, paint);

    using var image = surface.Snapshot();
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);
    byte[] imageBytes = data.ToArray();

    // --- 2. ส่งข้อมูลไปยัง Emulator (ESCPOS_NET v3.0.0 Syntax) ---
// --- 2. ส่งข้อมูลไปยัง Emulator (ใช้ TCP Direct - ชัวร์ที่สุด) ---
    try 
    {
        var emitter = new EPSON();

        // รวมคำสั่ง ESC/POS เป็น Byte Array
       byte[] commands = ByteSplicer.Combine(
    emitter.Initialize(),
    emitter.CenterAlign(),
    // ลองเปลี่ยนพารามิเตอร์โหมดภาพ หรือใช้คำสั่ง Raster หากโหมด Bitonal มีปัญหา
    emitter.PrintImage(imageBytes, true, false), // เพิ่มพารามิเตอร์ useHighDensity และ useRaster
    emitter.FeedLines(3), 
    emitter.FullCut()
);

        // ใช้ TcpClient ส่งข้อมูลตรงไปที่ Emulator Port 9100
        using (TcpClient client = new TcpClient("127.0.0.1", 9100))
        using (NetworkStream stream = client.GetStream())
        {
            await stream.WriteAsync(commands, 0, commands.Length);
            await stream.FlushAsync();
        }

        return Ok("พิมพ์สำเร็จ! เช็คที่ Emulator");
    }  catch (Exception ex)
    {
        return BadRequest($"Error: {ex.Message}");
    }
}
    

}
}