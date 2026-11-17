using Microsoft.EntityFrameworkCore;

namespace api.Models
{
    [PrimaryKey(nameof(CardPrintingId))]
    public class UserCard
    {
        public int CardPrintingId { get; set; }

        public int QuantityOwned { get; set; }
        public int QuantityWanted { get; set; }
        public int QuantityProxyOwned { get; set; }

        public CardPrinting CardPrinting { get; set; } = null!;
    }
}
