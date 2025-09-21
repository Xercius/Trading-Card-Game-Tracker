using Microsoft.EntityFrameworkCore;
using api.Models;

namespace api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Card> Cards { get; set; }
        public DbSet<CardPrinting> CardPrintings { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserCard> UserCards { get; set; }
        public DbSet<Deck> Decks { get; set; }
        public DbSet<DeckCard> DeckCards { get; set; }

        protected override void OnModelCreating(ModelBuilder b)
        {
            // Card → CardPrinting (unchanged)
            b.Entity<Card>()
             .HasMany(c => c.Printings)
             .WithOne(p => p.Card)
             .HasForeignKey(p => p.CardId)
             .OnDelete(DeleteBehavior.Cascade);

            // UserCard composite key
            b.Entity<UserCard>()
             .HasKey(uc => new { uc.UserId, uc.CardPrintingId });

            // UserCard → User
            b.Entity<UserCard>()
             .HasOne(uc => uc.User)
             .WithMany(u => u.UserCards)
             .HasForeignKey(uc => uc.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            // UserCard → CardPrinting
            b.Entity<UserCard>()
             .HasOne(uc => uc.CardPrinting)
             .WithMany()
             .HasForeignKey(uc => uc.CardPrintingId)
             .OnDelete(DeleteBehavior.Restrict);

            // Deck relations (keep these; do NOT reference User.UserCards here)
            b.Entity<Deck>()
             .HasOne(d => d.User)
             .WithMany() // no Decks nav on User
             .HasForeignKey(d => d.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            b.Entity<DeckCard>()
             .HasIndex(x => new { x.DeckId, x.CardPrintingId })
             .IsUnique();

            b.Entity<DeckCard>()
             .HasOne(dc => dc.Deck)
             .WithMany(d => d.Cards)
             .HasForeignKey(dc => dc.DeckId)
             .OnDelete(DeleteBehavior.Cascade);

            b.Entity<DeckCard>()
             .HasOne(dc => dc.CardPrinting)
             .WithMany()
             .HasForeignKey(dc => dc.CardPrintingId)
             .OnDelete(DeleteBehavior.Restrict);

        }
    }
}
