using Microsoft.EntityFrameworkCore;
using Buffet_Restaurant_Managment_System_API.Models;
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
        }
    }
}