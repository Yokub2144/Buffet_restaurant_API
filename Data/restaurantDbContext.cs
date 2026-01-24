using Microsoft.EntityFrameworkCore;
using Buffet_Restaurant_Managment_System_API.Models;
using Buffet_Restaurant_API.Models;
namespace Buffet_Restaurant_Managment_System_API.Data
{
    public class restaurantDbContext : DbContext
    {
        public restaurantDbContext(DbContextOptions<restaurantDbContext> options) : base(options)
        {
        }
        public DbSet<Member> Members { get; set; }
        public DbSet<Employee> Employee { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Employee>()
                .HasKey(e => e.Emp_id);
            modelBuilder.Entity<Member>()
                .HasKey(m => m.Member_id);
            modelBuilder.Entity<Employee>().ToTable("employee"); // หรือ "employees"
            modelBuilder.Entity<Member>().ToTable("member");
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Cart>().ToTable("Cart");
            modelBuilder.Entity<Cart_item>().ToTable("Cart_item");
            modelBuilder.Entity<Menu>().ToTable("Menu");
        }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<Cart_item> CartItems { get; set; }
        public DbSet<Menu> Menus { get; set; }

    }
}