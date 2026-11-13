using api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace api.Data.Configurations;

internal static class LookupConfigurationExtensions
{
    internal static void ConfigureLookup<TEntity>(
        this EntityTypeBuilder<TEntity> e,
        string table,
        string gameCollection)
        where TEntity : class
    {
        e.ToTable(table);
        e.HasKey("Id");
        e.Property<int>("GameId").IsRequired();
        e.Property<string>("Name").IsRequired().HasMaxLength(128);
        e.HasIndex("GameId", "Name")
            .IsUnique()
            .HasDatabaseName($"UX_{table}_GameId_Name");
        e.HasOne<Game>("Game")
            .WithMany(gameCollection)
            .HasForeignKey("GameId")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CardTypeConfiguration : IEntityTypeConfiguration<CardType>
{
    public void Configure(EntityTypeBuilder<CardType> e)
    {
        e.ConfigureLookup("CardTypes", nameof(Game.CardTypes));
        e.Property(t => t.Value).HasMaxLength(64);
    }
}

internal sealed class SubTypeConfiguration : IEntityTypeConfiguration<SubType>
{
    public void Configure(EntityTypeBuilder<SubType> e) =>
        e.ConfigureLookup("SubTypes", nameof(Game.SubTypes));
}

internal sealed class TraitConfiguration : IEntityTypeConfiguration<Trait>
{
    public void Configure(EntityTypeBuilder<Trait> e) =>
        e.ConfigureLookup("Traits", nameof(Game.Traits));
}

internal sealed class KeywordConfiguration : IEntityTypeConfiguration<Keyword>
{
    public void Configure(EntityTypeBuilder<Keyword> e) =>
        e.ConfigureLookup("Keywords", nameof(Game.Keywords));
}

internal sealed class ArenaConfiguration : IEntityTypeConfiguration<Arena>
{
    public void Configure(EntityTypeBuilder<Arena> e) =>
        e.ConfigureLookup("Arenas", nameof(Game.Arenas));
}

internal sealed class AspectConfiguration : IEntityTypeConfiguration<Aspect>
{
    public void Configure(EntityTypeBuilder<Aspect> e)
    {
        e.ConfigureLookup("Aspects", nameof(Game.Aspects));
        e.Property(a => a.Color).HasMaxLength(32);
    }
}

internal sealed class RarityConfiguration : IEntityTypeConfiguration<Rarity>
{
    public void Configure(EntityTypeBuilder<Rarity> e)
    {
        e.ConfigureLookup("Rarities", nameof(Game.Rarities));
        e.Property(r => r.Character).HasMaxLength(16);
        e.Property(r => r.Color).HasMaxLength(32);
        e.Property(r => r.RarityType).HasMaxLength(32);
    }
}

internal sealed class CardCardTypeConfiguration : IEntityTypeConfiguration<CardCardType>
{
    public void Configure(EntityTypeBuilder<CardCardType> e)
    {
        e.ToTable("CardCardTypes", t => t.HasCheckConstraint(
            "CK_CardCardTypes_SourceRole", "\"SourceRole\" IN ('Type', 'Type2')"));
        e.HasKey(x => new { x.CardId, x.CardTypeId, x.SourceRole });
        e.Property(x => x.SourceRole).IsRequired().HasMaxLength(5);
        // Cascade: join rows are owned by Card.
        e.HasOne(x => x.Card).WithMany(c => c.CardTypes)
            .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.CardType).WithMany(t => t.Cards)
            .HasForeignKey(x => x.CardTypeId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.CardTypeId).HasDatabaseName("IX_CardCardTypes_CardTypeId");
    }
}

internal sealed class CardSubTypeConfiguration : IEntityTypeConfiguration<CardSubType>
{
    public void Configure(EntityTypeBuilder<CardSubType> e)
    {
        e.ToTable("CardSubTypes");
        e.HasKey(x => new { x.CardId, x.SubTypeId });
        // Cascade: join rows are owned by Card.
        e.HasOne(x => x.Card).WithMany(c => c.CardSubTypes)
            .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.SubType).WithMany(t => t.Cards)
            .HasForeignKey(x => x.SubTypeId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.SubTypeId).HasDatabaseName("IX_CardSubTypes_SubTypeId");
    }
}

internal sealed class CardTraitConfiguration : IEntityTypeConfiguration<CardTrait>
{
    public void Configure(EntityTypeBuilder<CardTrait> e)
    {
        e.ToTable("CardTraits");
        e.HasKey(x => new { x.CardId, x.TraitId });
        // Cascade: join rows are owned by Card.
        e.HasOne(x => x.Card).WithMany(c => c.CardTraits)
            .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Trait).WithMany(t => t.Cards)
            .HasForeignKey(x => x.TraitId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.TraitId).HasDatabaseName("IX_CardTraits_TraitId");
    }
}

internal sealed class CardKeywordConfiguration : IEntityTypeConfiguration<CardKeyword>
{
    public void Configure(EntityTypeBuilder<CardKeyword> e)
    {
        e.ToTable("CardKeywords");
        e.HasKey(x => new { x.CardId, x.KeywordId });
        // Cascade: join rows are owned by Card.
        e.HasOne(x => x.Card).WithMany(c => c.CardKeywords)
            .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Keyword).WithMany(k => k.Cards)
            .HasForeignKey(x => x.KeywordId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.KeywordId).HasDatabaseName("IX_CardKeywords_KeywordId");
    }
}

internal sealed class CardArenaConfiguration : IEntityTypeConfiguration<CardArena>
{
    public void Configure(EntityTypeBuilder<CardArena> e)
    {
        e.ToTable("CardArenas");
        e.HasKey(x => new { x.CardId, x.ArenaId });
        // Cascade: join rows are owned by Card.
        e.HasOne(x => x.Card).WithMany(c => c.CardArenas)
            .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Arena).WithMany(a => a.Cards)
            .HasForeignKey(x => x.ArenaId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.ArenaId).HasDatabaseName("IX_CardArenas_ArenaId");
    }
}

internal sealed class CardAspectConfiguration : IEntityTypeConfiguration<CardAspect>
{
    public void Configure(EntityTypeBuilder<CardAspect> e)
    {
        e.ToTable("CardAspects", t => t.HasCheckConstraint(
            "CK_CardAspects_Count_Positive", "\"Count\" > 0"));
        e.HasKey(x => new { x.CardId, x.AspectId });
        // Cascade: join rows are owned by Card.
        e.HasOne(x => x.Card).WithMany(c => c.CardAspects)
            .HasForeignKey(x => x.CardId).OnDelete(DeleteBehavior.Cascade);
        e.HasOne(x => x.Aspect).WithMany(a => a.Cards)
            .HasForeignKey(x => x.AspectId).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => x.AspectId).HasDatabaseName("IX_CardAspects_AspectId");
    }
}
