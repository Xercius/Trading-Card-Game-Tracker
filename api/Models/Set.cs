using System.ComponentModel.DataAnnotations;

namespace api.Models;

public sealed class Set
{
    [Key]
    public int Id { get; set; }

    public int GameId { get; set; }
    public Game Game { get; set; } = null!;

    [Required]
    [MaxLength(32)]
    public required string Code { get; set; }

    [MaxLength(256)]
    public string? Name { get; set; }

    public DateOnly? ReleasedAt { get; set; }
    public DateTimeOffset LastSyncedAt { get; set; }

    public ICollection<SyncLog> SyncLogs { get; set; } = new List<SyncLog>();
}
