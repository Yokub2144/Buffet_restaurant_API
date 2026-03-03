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
        public DbSet<Tables> Tables { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Employee>()
                .HasKey(e => e.Emp_id);
            modelBuilder.Entity<Member>()
                .HasKey(m => m.Member_id);
            modelBuilder.Entity<Employee>().ToTable("employee");
            modelBuilder.Entity<Member>().ToTable("member");
            modelBuilder.Entity<Tables>()
                .HasKey(t => t.Table_id);
            base.OnModelCreating(modelBuilder);


            modelBuilder.Entity<Cart>().ToTable("Cart");
            modelBuilder.Entity<Cart_item>().ToTable("Cart_item");
            modelBuilder.Entity<Menu>().ToTable("Menu");

            modelBuilder.Entity<Booking>(e =>
            {
                e.ToTable("Booking");
                e.HasKey(x => x.Booking_id);
                e.HasOne(x => x.Member)
                 .WithMany()
                 .HasForeignKey(x => x.Member_id);
            });

            modelBuilder.Entity<GroupTable>(e =>
            {
                e.ToTable("GroupTables");
                e.HasKey(x => x.GroupTable_id);
                e.HasOne(x => x.Booking)
                 .WithMany(b => b.GroupTables)
                 .HasForeignKey(x => x.Booking_id);
                e.HasOne(x => x.Table)
                 .WithMany()
                 .HasForeignKey(x => x.Table_id);
            });
        }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<Cart_item> CartItems { get; set; }
        public DbSet<Menu> Menus { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<GroupTable> GroupTables { get; set; }
    }
}