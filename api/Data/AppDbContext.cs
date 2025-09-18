using api.Models;
using Microsoft.EntityFrameworkCore;

namespace api.Data // Update to match your namespace
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Fruit> Fruits { get; set; }
        public DbSet<Card> Cards { get; set; }
        public DbSet<CardPrinting> CardPrintings { get; set; }
        public DbSet<UserCard> UserCards { get; set; }
        public DbSet<User> Users { get; set; }
    }
}
