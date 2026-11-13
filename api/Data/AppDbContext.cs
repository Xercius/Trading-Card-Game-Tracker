using System.Reflection;
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
        public DbSet<Treatment> Treatments => Set<Treatment>();
        public DbSet<CardType> CardTypes => Set<CardType>();
        public DbSet<SubType> SubTypes => Set<SubType>();
        public DbSet<Trait> Traits => Set<Trait>();
        public DbSet<Keyword> Keywords => Set<Keyword>();
        public DbSet<Arena> Arenas => Set<Arena>();
        public DbSet<Aspect> Aspects => Set<Aspect>();
        public DbSet<Rarity> Rarities => Set<Rarity>();
        public DbSet<CardCardType> CardCardTypes => Set<CardCardType>();
        public DbSet<CardSubType> CardSubTypes => Set<CardSubType>();
        public DbSet<CardTrait> CardTraits => Set<CardTrait>();
        public DbSet<CardKeyword> CardKeywords => Set<CardKeyword>();
        public DbSet<CardArena> CardArenas => Set<CardArena>();
        public DbSet<CardAspect> CardAspects => Set<CardAspect>();
        public DbSet<SyncLog> SyncLogs => Set<SyncLog>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);

            b.HasDbFunction(typeof(AppDbContext).GetMethod(
                    nameof(SqliteJulianDay),
                    BindingFlags.NonPublic | BindingFlags.Static)!)
                .HasName("julianday")
                .IsBuiltIn();

            b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

        internal static double? SqliteJulianDay(DateTimeOffset? value)
            => throw new NotSupportedException();
    }
}
