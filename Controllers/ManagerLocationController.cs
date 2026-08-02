using Microsoft.AspNetCore.Mvc;
using Buffet_Restaurant_Managment_System_API.Services;
using Buffet_Restaurant_Managment_System_API.Dtos;

namespace Buffet_Restaurant_Managment_System_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ManagerLocationController : ControllerBase
    {
        [HttpPost("set-location")]
        public IActionResult SetShopLocation([FromBody] UpdateLocationDto request)
        {

            ShopLocationService.SaveLocation(request.Latitude, request.Longitude);

            return Ok(new
            {
                status = "success",
                message = "บันทึกพิกัดร้านเรียบร้อยแล้ว"
            });
        }


        [HttpGet("get-location")]
        public IActionResult GetShopLocation()
        {
            var location = ShopLocationService.GetLocation();
            if (location == null)
            {
                return NotFound(new { status = "error", message = "ยังไม่ได้ตั้งค่าพิกัด" });
            }
            return Ok(new { status = "success", data = location });
        }
    }
}