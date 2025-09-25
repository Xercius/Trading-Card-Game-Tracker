using Microsoft.EntityFrameworkCore;

namespace api.Models
{
    [PrimaryKey(nameof(UserId), nameof(CardPrintingId))]
    public class WishlistEntry
    {
        public int UserId { get; set; }
        public int CardPrintingId { get; set; }

        public int QuantityWanted { get; set; }

        public User User { get; set; } = null!;
        public CardPrinting CardPrinting { get; set; } = null!;
    }
}
