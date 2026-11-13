using api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace api.Data.Configurations;

internal sealed class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> e)
    {
        e.HasKey(c => c.Id);

        e.Property(c => c.Name).IsRequired().HasMaxLength(256);
        e.Property(c => c.Subtitle).HasMaxLength(256);
        e.Property(c => c.CardUid).HasMaxLength(64);
        e.Property(c => c.Hp).HasColumnName("Hp");
        e.Property(c => c.Side).HasMaxLength(16);
        e.Property(c => c.Uniqueness).HasMaxLength(16);
        e.Property(c => c.Destiny).HasMaxLength(16);
        e.Property(c => c.Deploy).HasMaxLength(16);
        e.Property(c => c.Forfeit).HasMaxLength(16);
        e.Property(c => c.Armor).HasMaxLength(16);
        e.Property(c => c.Ferocity).HasMaxLength(16);
        e.Property(c => c.Persona).HasMaxLength(256);
        e.Property(c => c.Stage).HasMaxLength(32);
        e.Property(c => c.BackTitle).HasMaxLength(256);

        // Non-unique: different mechanical cards in the same game can share a name (e.g., Pokemon, SWCCG).
        e.HasIndex(c => new { c.GameId, c.Name })
            .HasDatabaseName("IX_Cards_GameId_Name");
        e.HasIndex(c => new { c.GameId, c.Persona })
            .HasDatabaseName("IX_Cards_GameId_Persona");
        e.HasOne(c => c.Game)
            .WithMany()
            .HasForeignKey(c => c.GameId)
            .IsRequired()
            .OnDelete(DeleteBehavior.Restrict);

        // Normalized name for case-insensitive search.
        e.Property<string>("NameNormalized")
            .HasComputedColumnSql("UPPER(LTRIM(RTRIM([Name])))", stored: true);
        e.HasIndex("NameNormalized")
            .HasDatabaseName("IX_Cards_NameNormalized");

        // Cascade: CardPrintings are owned children of Card and cannot exist without it.
        e.HasMany(c => c.Printings)
            .WithOne(p => p.Card)
            .HasForeignKey(p => p.CardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CardPrintingConfiguration : IEntityTypeConfiguration<CardPrinting>
{
    public void Configure(EntityTypeBuilder<CardPrinting> e)
    {
        e.HasKey(p => p.Id);

        e.Property(p => p.Number).IsRequired().HasMaxLength(32);
        e.Property(p => p.ImageUrl).HasMaxLength(1024);
        e.Property(p => p.BackImageUrl).HasMaxLength(1024);
        e.Property(p => p.Artist).HasMaxLength(256);
        e.Property(p => p.CardUid).HasMaxLength(64);

        e.HasIndex(p => p.CardId)
            .HasDatabaseName("IX_CardPrintings_CardId");

        e.HasIndex(p => new { p.CardId, p.SetId, p.Number, p.TreatmentId })
            .IsUnique()
            .HasDatabaseName("UX_CardPrintings_CardId_SetId_Number_TreatmentId");

        e.HasOne(p => p.CatalogSet)
            .WithMany()
            .HasForeignKey(p => p.SetId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(p => p.SetId)
            .HasDatabaseName("IX_CardPrintings_SetId");

        e.HasOne(p => p.RarityLookup)
            .WithMany()
            .HasForeignKey(p => p.RarityId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(p => p.RarityId)
            .HasDatabaseName("IX_CardPrintings_RarityId");

        e.HasOne(p => p.Treatment)
            .WithMany(t => t.CardPrintings)
            .HasForeignKey(p => p.TreatmentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(p => p.TreatmentId)
            .HasDatabaseName("IX_CardPrintings_TreatmentId");

        e.HasOne(p => p.VariantOf).WithMany().HasForeignKey(p => p.VariantOfId)
            .OnDelete(DeleteBehavior.SetNull);
        e.HasOne(p => p.ReprintOf).WithMany().HasForeignKey(p => p.ReprintOfId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> e)
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
    }
}

internal sealed class SetConfiguration : IEntityTypeConfiguration<Set>
{
    public void Configure(EntityTypeBuilder<Set> e)
    {
        e.ToTable("Sets");
        e.HasKey(s => s.Id);
        e.Property(s => s.Code).IsRequired().HasMaxLength(32);
        e.Property(s => s.Name).HasMaxLength(256);
        e.Property(s => s.LogoUrl).HasMaxLength(1024);
        e.HasIndex(s => new { s.GameId, s.Code })
            .IsUnique()
            .HasDatabaseName("UX_Sets_GameId_Code");
        e.HasOne(s => s.Game)
            .WithMany(g => g.Sets)
            .HasForeignKey(s => s.GameId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TreatmentConfiguration : IEntityTypeConfiguration<Treatment>
{
    public void Configure(EntityTypeBuilder<Treatment> e)
    {
        e.ToTable("Treatments");
        e.HasKey(t => t.Id);
        e.Property(t => t.Name).IsRequired().HasMaxLength(128);
        e.Property(t => t.IconUrl).HasMaxLength(1024);
        e.Property(t => t.NameNormalized)
            .HasComputedColumnSql("LOWER(TRIM([Name]))", stored: true);
        e.HasIndex(t => new { t.GameId, t.NameNormalized })
            .IsUnique()
            .HasDatabaseName("UX_Treatments_GameId_NameNormalized");
        e.HasOne(t => t.Game)
            .WithMany(g => g.Treatments)
            .HasForeignKey(t => t.GameId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
