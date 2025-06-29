using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Models // Update to match your namespace
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Fruit> Fruits { get; set; }
    }
}
