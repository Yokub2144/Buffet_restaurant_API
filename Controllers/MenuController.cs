using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Buffet_Restaurant_API.Models;
using Buffet_Restaurant_API.Dtos;
using Buffet_Restaurant_Managment_System_API.Data;

namespace Buffet_Restaurant_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MenuController : ControllerBase
    {
        private readonly restaurantDbContext _context;

        public MenuController(restaurantDbContext context)
        {
            _context = context;
        }

        // 1. ดึงข้อมูลเมนูทั้งหมด 
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Menu>>> GetMenus()
        {
            return await _context.Menus.ToListAsync();
        }

        // 2. ดึงเมนูตาม ID
        [HttpGet("{id}")]
        public async Task<ActionResult<Menu>> GetMenu(int id)
        {
            var menu = await _context.Menus.FindAsync(id);

            if (menu == null)
            {
                return NotFound(new { message = "ไม่พบเมนูนี้ในระบบ" });
            }

            return menu;
        }

        // 3. เพิ่มเมนูใหม่ 
        [HttpPost]
        public async Task<ActionResult<Menu>> AddMenu([FromBody] MenuDto request)
        {
            var menu = new Menu
            {
                Menu_Name = request.Menu_Name,
                Price = request.Price,
                Category = request.Category,
                Menu_Image = request.Menu_Image,
                Menu_Type = request.Menu_Type
            };

            _context.Menus.Add(menu);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetMenu", new { id = menu.Menu_id }, menu);
        }
        // 4. แก้ไขเมนู 
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMenu(int id, [FromBody] MenuDto request)
        {
            var menu = await _context.Menus.FindAsync(id);
            if (menu == null)
            {
                return NotFound(new { message = "ไม่พบเมนูที่ต้องการแก้ไข" });
            }

            // อัปเดตข้อมูล
            menu.Menu_Name = request.Menu_Name;
            menu.Price = request.Price;
            menu.Category = request.Category;
            menu.Menu_Image = request.Menu_Image;
            menu.Menu_Type = request.Menu_Type;

            _context.Menus.Update(menu);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!MenuExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return Ok(new { message = "แก้ไขเมนูเรียบร้อยแล้ว", data = menu });
        }

        // 5. ลบเมนู 
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMenu(int id)
        {
            var menu = await _context.Menus.FindAsync(id);
            if (menu == null)
            {
                return NotFound(new { message = "ไม่พบเมนูที่ต้องการลบ" });
            }

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