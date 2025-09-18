namespace api.Models
{
    public class UserCard
    {
        public int Id { get; set; }

        public int UserId { get; set; }
        User User { get; set; }

        public int CardPrintingId { get; set; }
        public CardPrinting CardPrinting { get; set; } = null!;

        public int QuantityOwned { get; set; }
        public int QuantityWanted { get; set; } // for wishlist tracking
    }
}
