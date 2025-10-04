using Microsoft.EntityFrameworkCore;
using api.Models;

namespace api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Card> Cards => Set<Card>();
        public DbSet<CardPrinting> CardPrintings => Set<CardPrinting>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserCard> UserCards => Set<UserCard>();
        public DbSet<Deck> Decks => Set<Deck>();
        public DbSet<DeckCard> DeckCards => Set<DeckCard>();
        public DbSet<ValueHistory> ValueHistories => Set<ValueHistory>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.Entity<Card>()
                .HasKey(c => c.CardId);

            b.Entity<Deck>()
                .HasMany(d => d.Cards)
                .WithOne(dc => dc.Deck!)
                .HasForeignKey(dc => dc.DeckId)
                .OnDelete(DeleteBehavior.Cascade);

            b.Entity<DeckCard>()
                .HasOne(dc => dc.CardPrinting)
                .WithMany()
                .HasForeignKey(dc => dc.CardPrintingId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<UserCard>()
                .HasOne(uc => uc.CardPrinting)
                .WithMany()
                .HasForeignKey(uc => uc.CardPrintingId)
                .OnDelete(DeleteBehavior.Restrict);

            b.Entity<CardPrinting>()
                .HasIndex(p => new { p.CardId, p.Set, p.Number, p.Style })
                .IsUnique();

            b.Entity<DeckCard>()
                .HasIndex(dc => new { dc.DeckId, dc.CardPrintingId })
                .IsUnique();
        }
    }
}