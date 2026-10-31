using api.Models;

namespace api.Data;

/// <summary>
/// Provides a minimal seed that only ensures the default application owner exists.
/// </summary>
public static class MinimalDbSeeder
{
    public static void Seed(AppDbContext context)
    {
        if (!context.Users.Any(u => u.Id == DbSeeder.DefaultUserId))
        {
            context.Users.Add(new User { Id = DbSeeder.DefaultUserId, Username = "owner", DisplayName = "Owner" });
            context.SaveChanges();
        }
    }
}
