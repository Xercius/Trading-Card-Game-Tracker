using api.Models;

namespace api.Data
{
    public static class DbSeeder
    {
        /// <summary>
        /// The single application owner. All collection, deck, and wishlist data belongs to this user.
        /// </summary>
        public const int DefaultUserId = 1;

        public static void Seed(AppDbContext context)
        {
            // Ensure the single default user exists
            if (!context.Users.Any(u => u.Id == DefaultUserId))
            {
                context.Users.Add(new User { Id = DefaultUserId, Username = "owner", DisplayName = "Owner" });
                context.SaveChanges();
            }
        }
    }
}
