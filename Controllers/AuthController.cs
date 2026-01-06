using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Buffet_Restaurant_Managment_System_API.Data;
using Buffet_Restaurant_Managment_System_API.Models;
using Buffet_Restaurant_Managment_System_API.Dtos;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
namespace Buffet_Restaurant_Managment_System_API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly restaurantDbContext _context;
        private readonly IConfiguration _configuration;
        public AuthController(restaurantDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("login-employee")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> LoginEmployee([FromForm] loginEmployeeDtos loginData)
        {
            var employee = await _context.Employee
                .FirstOrDefaultAsync(e => e.Phone == loginData.Phone);

            if (employee == null)
            {
                return Unauthorized(new { Message = "ไม่พบข้อมูลผู้ใช้งาน หรือเบอร์โทรศัพท์ไม่ถูกต้อง" });
            }
            try 
            {
                if (!BCrypt.Net.BCrypt.Verify(loginData.Password, employee.Password))
                {
                    return Unauthorized(new { Message = "รหัสผ่านไม่ถูกต้อง" });
                }
            }       
            catch (BCrypt.Net.SaltParseException)
            {
                return BadRequest(new { Message = "รูปแบบรหัสผ่านในระบบไม่ถูกต้อง (ไม่ใช่ BCrypt)" });
            }
            if (employee.Employee_Status != "ทำงานปัจจุบัน")
            {
                return Unauthorized(new { Message = "ยังไม่ได้รับการอนุมัติการเป็นพนักงาน" });
            }

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, employee.Emp_id.ToString()),
                new Claim(ClaimTypes.Name, employee.Fullname?? "No Name"),
                new Claim(ClaimTypes.Role, employee.Department?? "พนักงาน")
            };
            var jwtKey = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(jwtKey))
            {
                return StatusCode(500, "ระบบผิดพลาด: ไม่พบการตั้งค่า Jwt:Key ใน appsettings.json");
        }
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(8),
                signingCredentials: creds);
            return Ok(new { Message = "เข้าสู่ระบบสำเร็จ", 
                            Token = new JwtSecurityTokenHandler().WriteToken(token), 
                            EmployeeName = employee.Fullname });
        }

        [HttpPost("login-member")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> LoginMember([FromForm] loginMemberDtos loginData)
        {
            var member = await _context.Members
                .FirstOrDefaultAsync(m => m.Phone == loginData.Phone);

            if (member == null || !BCrypt.Net.BCrypt.Verify(loginData.Password, member.Password))
            {
                return Unauthorized(new { Message = "เบอร์โทรศัพท์หรือรหัสผ่านไม่ถูกต้อง" });
            }
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, member.Member_id.ToString()),
                new Claim(ClaimTypes.Name, member.Fullname),
                new Claim(ClaimTypes.Role, "Member")
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: creds);

            return Ok(new { Message = "เข้าสู่ระบบสำเร็จ", 
                            Token = new JwtSecurityTokenHandler().WriteToken(token), 
                            MemberName = member.Fullname });
        }
        [HttpPost("register-employee")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult>registerEmployee([FromForm] resgisterEmployeeDtos employee, [FromServices] Cloudinary cloudinary)
        {
            if (await _context.Employee.AnyAsync(e => e.Phone == employee.Phone))
            {
                return BadRequest(new { Message = "เบอร์โทรศัพท์นี้ถูกใช้งานแล้ว" });
            }

            if (await _context.Employee.AnyAsync(e => e.Email == employee.Email))
            {
                return BadRequest(new { Message = "อีเมลนี้ถูกใช้งานแล้ว" });
            }

            if (await _context.Employee.AnyAsync(e => e.Identification_Number == employee.Identification_Number))
            {
                return BadRequest(new { Message = "เลขบัตรประชาชนนี้ถูกใช้งานแล้ว" });
            }
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(employee.Password);

            var newEmployee = new Employee
            {
                Fullname = employee.Fullname,
                Email = employee.Email,
                Phone = employee.Phone,
                Password = passwordHash,
                Gender = employee.Gender,
                Identification_Number = employee.Identification_Number,
                Address = employee.Address,
                Image_Profile = "",
                Department = employee.Department,
                Employee_Type = employee.Employee_Type
            };

            if (employee.Image_Profile != null && employee.Image_Profile.Length > 0)
            {
      
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                var extension = Path.GetExtension(employee.Image_Profile.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                    return BadRequest(new { message = "กรุณาอัปโหลดไฟล์รูปภาพ (.jpg, .png) เท่านั้น" });

                    using var stream = employee.Image_Profile.OpenReadStream();
                    var uploadParams = new ImageUploadParams()
                {
                    File = new FileDescription(employee.Image_Profile.FileName, stream),
                    Folder = "Employee_profiles", // จัดกลุ่มรูปภาพไว้ใน Folder
                    PublicId = $"Employee_{Guid.NewGuid()}",
           
                    Transformation = new Transformation().Width(500).Height(500).Crop("fill").Gravity("face")
                };

                     var uploadResult = await cloudinary.UploadAsync(uploadParams);

                if (uploadResult.Error != null)
                {
                    return BadRequest(new { message = $"อัปโหลดรูปภาพไม่สำเร็จ: {uploadResult.Error.Message}" });
                }

                newEmployee.Image_Profile = uploadResult.SecureUrl.ToString();
            }
            _context.Employee.Add(newEmployee);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "สมัครสมาชิกพนักงานสำเร็จ", Employee = newEmployee });
        }
        [HttpPost("register-member")]
        [Consumes("application/json")]
        public async Task<IActionResult> registerMember([FromBody] registerMemberDtos member)
        {
            if (await _context.Members.AnyAsync(m => m.Email == member.Email))
            {
                return BadRequest(new { Message = "อีเมลนี้ถูกใช้งานแล้ว" });
            }
            if (await _context.Members.AnyAsync(m => m.Phone == member.Phone))
            {
                return BadRequest(new { Message = "เบอร์โทรศัพท์นี้ถูกใช้งานแล้ว" });
            }
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(member.Password);
            var newMember = new Member
            {
                Fullname = member.Fullname,
                Email = member.Email,
                Phone = member.Phone,
                Password = passwordHash,
                Birthday = member.Birthday
            };

            _context.Members.Add(newMember);
            await _context.SaveChangesAsync();

            return Ok(new { Message = "สมัครสมาชิกสำเร็จ", Member = newMember });
        }
    }
}