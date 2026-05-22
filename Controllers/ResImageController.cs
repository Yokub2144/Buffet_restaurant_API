using Buffet_Restaurant_Managment_System_API.Models;
using Buffet_Restaurant_Managment_System_API.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using BUFFET_RESTAURANT_API.Models;
using Microsoft.AspNetCore.SignalR;
using Buffet_Restaurant_Managment_System_API.Hubs;

namespace Buffet_Restaurant_Managment_System_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ResImageController : ControllerBase
    {
        private readonly restaurantDbContext _context;
        private readonly Cloudinary _cloudinary;
        private readonly IHubContext<tableStatusHub> _hubContext;

        public ResImageController(restaurantDbContext context, Cloudinary cloudinary, IHubContext<tableStatusHub> hubContext)
        {
            _context = context;
            _cloudinary = cloudinary;
            _hubContext = hubContext;
        }

        // ดึง public_id จาก Cloudinary URL เพื่อลบรูปออกจาก Cloudinary
        private string? ExtractPublicId(string imageUrl)
        {
            try
            {
                var uri = new Uri(imageUrl);
                var segments = uri.AbsolutePath.Split('/');
                var uploadIndex = Array.IndexOf(segments, "upload");
                if (uploadIndex < 0) return null;

                var startIndex = uploadIndex + 1;
                if (startIndex < segments.Length &&
                    System.Text.RegularExpressions.Regex.IsMatch(segments[startIndex], @"^v\d+$"))
                    startIndex++;

                var publicIdWithExt = string.Join("/", segments[startIndex..]);
                var dotIndex = publicIdWithExt.LastIndexOf('.');
                return dotIndex > 0 ? publicIdWithExt[..dotIndex] : publicIdWithExt;
            }
            catch { return null; }
        }

        private async Task DeleteFromCloudinaryAsync(string imageUrl)
        {
            var publicId = ExtractPublicId(imageUrl);
            if (!string.IsNullOrEmpty(publicId))
                await _cloudinary.DestroyAsync(new DeletionParams(publicId));
        }

        // GET /api/ResImage - ดึงรูปทั้งหมด
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ResImage>>> GetImages()
        {
            return await _context.Res_Image.ToListAsync();
        }

        // POST /api/ResImage/upload
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult<ResImage>> AddImage([FromForm] ResImageUploadDto request)
        {
            try
            {
                if (request.ImageFile == null)
                    return BadRequest(new { message = "กรุณาเลือกไฟล์รูปภาพ" });

                using var stream = request.ImageFile.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(request.ImageFile.FileName, stream),
                    Folder = "Restaurant_Assets",
                    PublicId = $"{request.Image_Type}_{Guid.NewGuid()}"
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.Error != null)
                    return BadRequest(new { message = uploadResult.Error.Message });

                var newImage = new ResImage
                {
                    Image_Url = uploadResult.SecureUrl.ToString(),
                    Image_Type = request.Image_Type
                };

                _context.Res_Image.Add(newImage);
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("UpdateResImage");

                return Ok(new { message = "เพิ่มรูปภาพสำเร็จ", data = newImage });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // PUT /api/ResImage/update/{id}
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpPut("update/{id}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UpdateImage(int id, [FromForm] ResImageUploadDto request)
        {
            try
            {
                var imageInDb = await _context.Res_Image.FindAsync(id);
                if (imageInDb == null)
                    return NotFound(new { message = "ไม่พบรูปภาพ" });

                if (request.ImageFile != null)
                {
                    // ลบรูปเก่าออกจาก Cloudinary 
                    await DeleteFromCloudinaryAsync(imageInDb.Image_Url);

                    using var stream = request.ImageFile.OpenReadStream();
                    var uploadParams = new ImageUploadParams
                    {
                        File = new FileDescription(request.ImageFile.FileName, stream),
                        Folder = "Restaurant_Assets",
                        PublicId = $"{request.Image_Type}_{id}_{Guid.NewGuid()}"
                    };

                    var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                    if (uploadResult.Error != null)
                        return BadRequest(new { message = uploadResult.Error.Message });

                    imageInDb.Image_Url = uploadResult.SecureUrl.ToString();
                }

                imageInDb.Image_Type = request.Image_Type ?? imageInDb.Image_Type;
                await _context.SaveChangesAsync();

                await _hubContext.Clients.All.SendAsync("UpdateResImage");

                return Ok(new { message = "แก้ไขรูปภาพสำเร็จ", data = imageInDb });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }

        // DELETE /api/ResImage/{id}
        [Authorize(Roles = "เจ้าของร้าน")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteImage(int id)
        {
            try
            {
                var image = await _context.Res_Image.FindAsync(id);
                if (image == null)
                    return NotFound(new { message = "ไม่พบรูปภาพ" });

                // ลบออกจาก Cloudinary ก่อน
                await DeleteFromCloudinaryAsync(image.Image_Url);

                _context.Res_Image.Remove(image);
                await _context.SaveChangesAsync();
                await _hubContext.Clients.All.SendAsync("UpdateResImage");

                return Ok(new { message = "ลบรูปเรียบร้อยแล้ว" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = ex.Message });
            }
        }
    }
}