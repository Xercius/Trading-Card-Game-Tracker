using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace api.Models
{
    public enum ValueScopeType { CardPrinting = 1, Deck = 2, Collection = 3 }

    [Index(nameof(ScopeType), nameof(ScopeId), nameof(AsOfUtc))]
    public class ValueHistory
    {
        [Key] public int Id { get; set; }

        public ValueScopeType ScopeType { get; set; }
        public int ScopeId { get; set; }

        public long PriceCents { get; set; }
        public DateTime AsOfUtc { get; set; }
        public string Source { get; set; } = "manual";
    }
}
