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
        public DbSet<ImportSyncHistory> ImportSyncHistories => Set<ImportSyncHistory>();
        public DbSet<Game> Games => Set<Game>();
        public DbSet<Set> Sets => Set<Set>();
        public DbSet<CardType> CardTypes => Set<CardType>();
        public DbSet<Trait> Traits => Set<Trait>();
        public DbSet<Keyword> Keywords => Set<Keyword>();
        public DbSet<Arena> Arenas => Set<Arena>();
        public DbSet<Aspect> Aspects => Set<Aspect>();
        public DbSet<Rarity> Rarities => Set<Rarity>();
        public DbSet<CardCardType> CardCardTypes => Set<CardCardType>();
        public DbSet<CardTrait> CardTraits => Set<CardTrait>();
        public DbSet<CardKeyword> CardKeywords => Set<CardKeyword>();
        public DbSet<CardArena> CardArenas => Set<CardArena>();
        public DbSet<CardAspect> CardAspects => Set<CardAspect>();
        public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

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
                e.Property(c => c.Subtitle).HasMaxLength(256);
                e.Property(c => c.CardType2).HasMaxLength(64);
                e.Property(c => c.CardUid).HasMaxLength(64);
                e.Property(c => c.Arena).HasMaxLength(32);
                e.Property(c => c.Artist).HasMaxLength(128);
                e.Property(c => c.Aspects).HasMaxLength(256);
                e.Property(c => c.Traits).HasMaxLength(512);
                e.Property(c => c.Keywords).HasMaxLength(512);
                e.Property(c => c.Hp).HasColumnName("Hp");

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

                // self-referencing FK: a variant card points to its base card
                e.HasOne(c => c.BaseCard)
                 .WithMany(c => c.Variants)
                 .HasForeignKey(c => c.BaseCardId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasIndex(c => c.BaseCardId);
                e.HasIndex(c => new { c.Game, c.StrapiId })
                    .IsUnique()
                    .HasFilter("\"StrapiId\" IS NOT NULL");

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
                e.Property(p => p.BackImageUrl).HasMaxLength(1024);
                e.Property(p => p.CardUid).HasMaxLength(64);

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

                e.HasOne(p => p.CatalogSet)
                    .WithMany()
                    .HasForeignKey(p => p.SetId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(p => p.SetId);

                e.HasOne(p => p.RarityLookup)
                    .WithMany()
                    .HasForeignKey(p => p.RarityId)
                    .IsRequired(false)
                    .OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(p => p.RarityId);
                e.HasIndex(p => p.StrapiId)
                    .IsUnique()
                    .HasFilter("\"StrapiId\" IS NOT NULL");
                e.HasOne(p => p.VariantOf).WithMany().HasForeignKey(p => p.VariantOfId)
                    .OnDelete(DeleteBehavior.SetNull);
                e.HasOne(p => p.ReprintOf).WithMany().HasForeignKey(p => p.ReprintOfId)
                    .OnDelete(DeleteBehavior.SetNull);
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

            b.Entity<ImportSyncHistory>(e =>
            {
                e.HasKey(h => h.Id);
                e.Property(h => h.ImporterKey).IsRequired().HasMaxLength(64);
                e.Property(h => h.SetCode).IsRequired().HasMaxLength(64);

                // one row per (importer key, set code) combination
                e.HasIndex(h => new { h.ImporterKey, h.SetCode })
                 .IsUnique();
            });

            b.Entity<Game>(e =>
            {
                e.ToTable("Games");
                e.HasKey(g => g.Id);
                e.Property(g => g.Name).IsRequired().HasMaxLength(128);
                e.Property(g => g.NameNormalized)
                    .HasComputedColumnSql("LOWER(TRIM([Name]))", stored: true);
                e.HasIndex(g => g.NameNormalized)
                    .IsUnique()
                    .HasDatabaseName("UX_Games_NameNormalized");
                e.HasData(new Game { Id = 1, Name = "Star Wars Unlimited" });
            });

            b.Entity<Set>(e =>
            {
                e.ToTable("Sets");
                e.HasKey(s => s.Id);
                e.Property(s => s.Code).IsRequired().HasMaxLength(32);
                e.Property(s => s.Name).HasMaxLength(256);
                e.HasIndex(s => new { s.GameId, s.Code })
                    .IsUnique()
                    .HasDatabaseName("UX_Sets_GameId_Code");
                e.HasOne(s => s.Game)
                    .WithMany(g => g.Sets)
                    .HasForeignKey(s => s.GameId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            ConfigureLookup(b.Entity<CardType>(), "CardTypes", "CardTypes");
            ConfigureLookup(b.Entity<Trait>(), "Traits", "Traits");
            ConfigureLookup(b.Entity<Keyword>(), "Keywords", "Keywords");
            ConfigureLookup(b.Entity<Arena>(), "Arenas", "Arenas");
            ConfigureLookup(b.Entity<Aspect>(), "Aspects", "Aspects");
            ConfigureLookup(b.Entity<Rarity>(), "Rarities", "Rarities");

            b.Entity<CardType>().Property(t => t.Value).HasMaxLength(64);
            b.Entity<Aspect>().Property(a => a.Color).HasMaxLength(32);
            b.Entity<Rarity>(e =>
            {
                e.Property(r => r.Character).HasMaxLength(1);
                e.Property(r => r.Color).HasMaxLength(32);
            });

            b.Entity<CardCardType>(e =>
            {
                e.ToTable("CardCardTypes", t => t.HasCheckConstraint(
                    "CK_CardCardTypes_SourceRole", "\"SourceRole\" IN ('Type', 'Type2')"));
                e.HasKey(x => new { x.CardId, x.CardTypeId, x.SourceRole });
                e.Property(x => x.SourceRole).IsRequired().HasMaxLength(5);
                e.HasOne(x => x.Card).WithMany(c => c.CardTypes)
                    .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.CardType).WithMany(t => t.Cards)
                    .HasForeignKey(x => x.CardTypeId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.CardTypeId);
            });

            b.Entity<CardTrait>(e =>
            {
                e.ToTable("CardTraits");
                e.HasKey(x => new { x.CardId, x.TraitId });
                e.HasOne(x => x.Card).WithMany(c => c.CardTraits)
                    .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Trait).WithMany(t => t.Cards)
                    .HasForeignKey(x => x.TraitId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.TraitId);
            });

            b.Entity<CardKeyword>(e =>
            {
                e.ToTable("CardKeywords");
                e.HasKey(x => new { x.CardId, x.KeywordId });
                e.HasOne(x => x.Card).WithMany(c => c.CardKeywords)
                    .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Keyword).WithMany(k => k.Cards)
                    .HasForeignKey(x => x.KeywordId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.KeywordId);
            });

            b.Entity<CardArena>(e =>
            {
                e.ToTable("CardArenas");
                e.HasKey(x => new { x.CardId, x.ArenaId });
                e.HasOne(x => x.Card).WithMany(c => c.CardArenas)
                    .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Arena).WithMany(a => a.Cards)
                    .HasForeignKey(x => x.ArenaId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.ArenaId);
            });

            b.Entity<CardAspect>(e =>
            {
                e.ToTable("CardAspects", t => t.HasCheckConstraint(
                    "CK_CardAspects_Count_Positive", "\"Count\" > 0"));
                e.HasKey(x => new { x.CardId, x.AspectId });
                e.HasOne(x => x.Card).WithMany(c => c.CardAspects)
                    .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Aspect).WithMany(a => a.Cards)
                    .HasForeignKey(x => x.AspectId).OnDelete(DeleteBehavior.Restrict);
                e.HasIndex(x => x.AspectId);
            });

            // --- SyncLog (SWU import operation audit log) ---
            b.Entity<SyncLog>(e =>
            {
                e.ToTable("SyncLogs");
                e.HasKey(l => l.Id);

                e.Property(l => l.Status).IsRequired().HasMaxLength(16);
                e.Property(l => l.CatalogScope).HasMaxLength(32);

                // fast lookup by set + time
                e.HasIndex(l => new { l.SetId, l.StartedAt });

                // nullable FK to shared Set
                e.HasOne(l => l.Set)
                 .WithMany(s => s.SyncLogs)
                 .HasForeignKey(l => l.SetId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.SetNull);
            });
        }

        private static void ConfigureLookup<TEntity>(
            Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> e,
            string table,
            string gameCollection)
            where TEntity : class
        {
            e.ToTable(table);
            e.HasKey("Id");
            e.Property<int>("GameId").IsRequired();
            e.Property<string>("Name").IsRequired().HasMaxLength(128);
            e.HasIndex("GameId", "Name").IsUnique();
            e.HasOne<Game>("Game")
                .WithMany(gameCollection)
                .HasForeignKey("GameId")
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
