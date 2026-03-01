using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Buffet_Restaurant_API.Models;
using Buffet_Restaurant_API.Dtos;
using Buffet_Restaurant_Managment_System_API.Data;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace Buffet_Restaurant_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MenuController : ControllerBase
    {
        private readonly restaurantDbContext _context;
        private readonly Cloudinary _cloudinary;

        public MenuController(restaurantDbContext context, Cloudinary cloudinary)
        {
            _context = context;
            _cloudinary = cloudinary;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Menu>>> GetMenus()
        {
            return await _context.Menus.ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Menu>> GetMenu(int id)
        {
            var menu = await _context.Menus.FindAsync(id);
            if (menu == null) return NotFound(new { message = "ไม่พบเมนูนี้ในระบบ" });
            return menu;
        }


        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<Menu>> AddMenu([FromForm] MenuFormDto request)
        {
            try
            {
                var menu = new Menu
                {
                    Menu_Name = request.Menu_Name,
                    Price = request.Price,
                    Category = request.Category,
                    Menu_Type = request.Menu_Type,
                    Menu_Image = "default.png"
                };

                if (request.ImageFile != null && request.ImageFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    var extension = Path.GetExtension(request.ImageFile.FileName).ToLower();
                    if (!allowedExtensions.Contains(extension))
                        return BadRequest(new { message = "กรุณาอัปโหลดไฟล์รูปภาพ (.jpg, .png) เท่านั้น" });

                    using var stream = request.ImageFile.OpenReadStream();
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(request.ImageFile.FileName, stream),
                        Folder = "Menu_images",
                        PublicId = $"Menu_{Guid.NewGuid()}",
                        Transformation = new Transformation().Width(600).Height(600).Crop("fill")
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                    if (uploadResult.Error != null)
                        return BadRequest(new { message = $"อัปโหลดรูปลง Cloudinary ไม่สำเร็จ: {uploadResult.Error.Message}" });

                    menu.Menu_Image = uploadResult.SecureUrl.ToString();
                }

                _context.Menus.Add(menu);
                await _context.SaveChangesAsync();

                return CreatedAtAction("GetMenu", new { id = menu.Menu_id }, menu);
            }
            catch (Exception ex)
            {
                var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = $"เกิดข้อผิดพลาดจาก Backend: {errorMessage}" });
            }
        }

        [HttpPut("{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateMenu(int id, [FromForm] MenuFormDto request)
        {
            try
            {
                var menu = await _context.Menus.FindAsync(id);
                if (menu == null) return NotFound(new { message = "ไม่พบเมนูที่ต้องการแก้ไข" });

                menu.Menu_Name = request.Menu_Name;
                menu.Price = request.Price;
                menu.Category = request.Category;
                menu.Menu_Type = request.Menu_Type;

                if (request.ImageFile != null && request.ImageFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                    var extension = Path.GetExtension(request.ImageFile.FileName).ToLower();
                    if (!allowedExtensions.Contains(extension))
                        return BadRequest(new { message = "กรุณาอัปโหลดไฟล์รูปภาพ (.jpg, .png) เท่านั้น" });

                    using var stream = request.ImageFile.OpenReadStream();
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(request.ImageFile.FileName, stream),
                        Folder = "Menu_images",
                        PublicId = $"Menu_{Guid.NewGuid()}",
                        Transformation = new Transformation().Width(600).Height(600).Crop("fill")
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);

                    if (uploadResult.Error != null)
                        return BadRequest(new { message = $"อัปโหลดรูปลง Cloudinary ไม่สำเร็จ: {uploadResult.Error.Message}" });

                    menu.Menu_Image = uploadResult.SecureUrl.ToString();
                }

                _context.Menus.Update(menu);
                await _context.SaveChangesAsync();

                return Ok(new { message = "แก้ไขเมนูเรียบร้อยแล้ว", data = menu });
            }
            catch (Exception ex)
            {
                var errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = $"เกิดข้อผิดพลาดจาก Backend: {errorMessage}" });
            }
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMenu(int id)
        {
            var menu = await _context.Menus.FindAsync(id);
            if (menu == null) return NotFound(new { message = "ไม่พบเมนูที่ต้องการลบ" });

            _context.Menus.Remove(menu);
            await _context.SaveChangesAsync();

            return Ok(new { message = "ลบเมนูเรียบร้อยแล้ว" });
        }

        private bool MenuExists(int id)
        {
            return _context.Menus.Any(e => e.Menu_id == id);
        }
    }
}