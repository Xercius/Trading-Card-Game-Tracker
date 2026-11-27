using api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace api.Data.Configurations;

internal sealed class DeckConfiguration : IEntityTypeConfiguration<Deck>
{
    public void Configure(EntityTypeBuilder<Deck> e)
    {
        e.HasKey(d => d.Id);
        e.Property(d => d.Game).IsRequired().HasMaxLength(80);

        e.HasOne(d => d.GameNavigation)
            .WithMany()
            .HasForeignKey(d => d.GameId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(d => d.GameId).HasDatabaseName("IX_Decks_GameId");

        // Cascade: DeckCard rows are owned children of Deck.
        e.HasMany(d => d.Cards)
            .WithOne(dc => dc.Deck!)
            .HasForeignKey(dc => dc.DeckId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DeckCardConfiguration : IEntityTypeConfiguration<DeckCard>
{
    public void Configure(EntityTypeBuilder<DeckCard> e)
    {
        e.HasKey(dc => dc.Id);

        e.HasOne(dc => dc.CardPrinting)
            .WithMany(p => p.DeckCards)
            .HasForeignKey(dc => dc.CardPrintingId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(dc => new { dc.DeckId, dc.CardPrintingId })
            .IsUnique()
            .HasDatabaseName("UX_DeckCards_DeckId_CardPrintingId");
    }
}

internal sealed class UserCardConfiguration : IEntityTypeConfiguration<UserCard>
{
    public void Configure(EntityTypeBuilder<UserCard> e)
    {
        e.HasKey(uc => uc.CardPrintingId);

        e.Property(uc => uc.QuantityOwned).HasDefaultValue(0);
        e.Property(uc => uc.QuantityWanted).HasDefaultValue(0);
        e.Property(uc => uc.QuantityProxyOwned).HasDefaultValue(0);

        e.HasOne(uc => uc.CardPrinting)
            .WithMany()
            .HasForeignKey(uc => uc.CardPrintingId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class WishlistSourceConfiguration : IEntityTypeConfiguration<WishlistSource>
{
    public void Configure(EntityTypeBuilder<WishlistSource> e)
    {
        e.ToTable("WishlistSources");
        e.HasKey(w => w.Id);

        e.Property(w => w.Quantity).HasDefaultValue(0);

        // Cascade: wishlist source rows are owned by the printing if a printing is removed.
        e.HasOne(w => w.CardPrinting)
            .WithMany()
            .HasForeignKey(w => w.CardPrintingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: DeleteDeckCore explicitly removes deck wishlist sources and recalculates UserCard.QuantityWanted.
        e.HasOne(w => w.Deck)
            .WithMany()
            .HasForeignKey(w => w.DeckId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(w => w.DeckId)
            .HasDatabaseName("IX_WishlistSources_DeckId");

        e.HasIndex(w => new { w.CardPrintingId, w.DeckId })
            .IsUnique()
            .HasFilter("[DeckId] IS NOT NULL")
            .HasDatabaseName("UX_WishlistSources_CardPrintingId_DeckId");

        e.HasIndex(w => w.CardPrintingId)
            .IsUnique()
            .HasFilter("[DeckId] IS NULL")
            .HasDatabaseName("UX_WishlistSources_CardPrintingId_General");
    }
}

internal sealed class ValueHistoryConfiguration : IEntityTypeConfiguration<ValueHistory>
{
    public void Configure(EntityTypeBuilder<ValueHistory> e)
    {
        e.HasKey(v => v.Id);
        e.Property(v => v.Source).IsRequired().HasMaxLength(64);
    }
}

internal sealed class CardPriceHistoryConfiguration : IEntityTypeConfiguration<CardPriceHistory>
{
    public void Configure(EntityTypeBuilder<CardPriceHistory> e)
    {
        e.HasKey(p => p.Id);

        // Cascade: price snapshots are owned history of CardPrinting.
        e.HasOne(p => p.CardPrinting)
            .WithMany()
            .HasForeignKey(p => p.CardPrintingId)
            .OnDelete(DeleteBehavior.Cascade);

        e.HasIndex(p => new { p.CardPrintingId, p.CapturedAt })
            .IsUnique()
            .HasDatabaseName("UX_CardPriceHistories_CardPrintingId_CapturedAt");

        e.Property(p => p.Price)
            .HasColumnType("decimal(14,2)");
    }
}

internal sealed class ImportSyncHistoryConfiguration : IEntityTypeConfiguration<ImportSyncHistory>
{
    public void Configure(EntityTypeBuilder<ImportSyncHistory> e)
    {
        e.HasKey(h => h.Id);
        e.Property(h => h.ImporterKey).IsRequired().HasMaxLength(64);
        e.Property(h => h.SetCode).IsRequired().HasMaxLength(64);

        e.HasIndex(h => new { h.ImporterKey, h.SetCode })
            .IsUnique()
            .HasDatabaseName("UX_ImportSyncHistories_ImporterKey_SetCode");
    }
}

internal sealed class SyncLogConfiguration : IEntityTypeConfiguration<SyncLog>
{
    public void Configure(EntityTypeBuilder<SyncLog> e)
    {
        e.ToTable("SyncLogs");
        e.HasKey(l => l.Id);

        e.Property(l => l.Status).IsRequired().HasMaxLength(16);
        e.Property(l => l.CatalogScope).HasMaxLength(32);

        e.HasIndex(l => new { l.SetId, l.StartedAt })
            .HasDatabaseName("IX_SyncLogs_SetId_StartedAt");

        e.HasOne(l => l.Set)
            .WithMany(s => s.SyncLogs)
            .HasForeignKey(l => l.SetId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
