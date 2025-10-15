using api.Models;
using Microsoft.EntityFrameworkCore;

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
        public DbSet<CardPriceHistory> CardPriceHistories => Set<CardPriceHistory>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            // --- Card (unique card) ---
            b.Entity<Card>(e =>
            {
                e.HasKey(c => c.Id);

                // required columns (don’t rely only on C# 'required' for EF)
                e.Property(c => c.Game).IsRequired().HasMaxLength(64);
                e.Property(c => c.Name).IsRequired().HasMaxLength(256);
                e.Property(c => c.CardType).IsRequired().HasMaxLength(128);

                // useful lookup index for list/search
                e.HasIndex(c => new { c.Game, c.Name });

                // normalized name for case-insensitive search (SQL Server shown)
                e.Property<string>("NameNormalized")
                 .HasComputedColumnSql("UPPER(LTRIM(RTRIM([Name])))", stored: true);
                e.HasIndex("NameNormalized");

                // normalized game for case-insensitive search
                e.Property<string>("GameNorm")
                 .HasComputedColumnSql("LOWER(TRIM([Game]))", stored: true);
                e.HasIndex("GameNorm");

                // relationship to printings
                e.HasMany(c => c.Printings)
                 .WithOne(p => p.Card)
                 .HasForeignKey(p => p.CardId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            // --- CardPrinting (specific printing/version) ---
            b.Entity<CardPrinting>(e =>
            {
                e.HasKey(p => p.Id);

                e.Property(p => p.Set).IsRequired().HasMaxLength(64);
                e.Property(p => p.Number).IsRequired().HasMaxLength(32);
                e.Property(p => p.Rarity).IsRequired().HasMaxLength(32);
                e.Property(p => p.Style).IsRequired().HasMaxLength(64);

                // fast FK lookup
                e.HasIndex(p => p.CardId);

                // filter/sort by set+number is super common
                e.HasIndex(p => new { p.Set, p.Number });

                // normalized set for case-insensitive search
                e.Property<string>("SetNorm")
                 .HasComputedColumnSql("LOWER(TRIM([Set]))", stored: true);
                e.HasIndex("SetNorm");

                // normalized rarity for case-insensitive search
                e.Property<string>("RarityNorm")
                 .HasComputedColumnSql("LOWER(TRIM([Rarity]))", stored: true);
                e.HasIndex("RarityNorm");

                // normalized style for case-insensitive search
                e.Property<string>("StyleNorm")
                 .HasComputedColumnSql("LOWER(TRIM([Style]))", stored: true);
                e.HasIndex("StyleNorm");

                // one logical printing per (Card, Set, Number, Style)
                e.HasIndex(p => new { p.CardId, p.Set, p.Number, p.Style })
                 .IsUnique();
            });

            // --- Deck / DeckCard ---
            b.Entity<Deck>(e =>
            {
                e.HasKey(d => d.Id);
                e.HasMany(d => d.Cards)
                 .WithOne(dc => dc.Deck!)
                 .HasForeignKey(dc => dc.DeckId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            b.Entity<DeckCard>(e =>
            {
                e.HasKey(dc => dc.Id);

                // prevent deleting a printing that is referenced by a deck
                e.HasOne(dc => dc.CardPrinting)
                 .WithMany(p => p.DeckCards)
                 .HasForeignKey(dc => dc.CardPrintingId)
                 .OnDelete(DeleteBehavior.Restrict);

                // one row per (deck, printing)
                e.HasIndex(dc => new { dc.DeckId, dc.CardPrintingId })
                 .IsUnique();
            });

            // --- UserCard (collection/wishlist entries) ---
            b.Entity<UserCard>(e =>
            {
                // Data annotation [PrimaryKey(UserId, CardPrintingId)] already exists,
                // but keep this for clarity in the fluent model:
                e.HasKey(uc => new { uc.UserId, uc.CardPrintingId });

                e.Property(uc => uc.QuantityOwned).HasDefaultValue(0);
                e.Property(uc => uc.QuantityWanted).HasDefaultValue(0);
                e.Property(uc => uc.QuantityProxyOwned).HasDefaultValue(0);

                e.HasOne(uc => uc.User)
                 .WithMany(u => u.UserCards)
                 .HasForeignKey(uc => uc.UserId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(uc => uc.CardPrinting)
                 .WithMany()
                 .HasForeignKey(uc => uc.CardPrintingId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // --- ValueHistory (if present) ---
            b.Entity<ValueHistory>(e =>
            {
                e.HasKey(v => v.Id);
                // If ValueHistory links to CardPrinting, add FK + index here.
                // e.HasIndex(v => new { v.CardPrintingId, v.Date });
            });

            b.Entity<CardPriceHistory>(e =>
            {
                e.HasKey(p => p.Id);

                e.HasOne(p => p.CardPrinting)
                    .WithMany()
                    .HasForeignKey(p => p.CardPrintingId)
                    .OnDelete(DeleteBehavior.Cascade);

                e.Property(p => p.Price)
                    .HasColumnType("decimal(14,2)");
            });
        }
    }
}
