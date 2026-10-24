using api.Data;
using api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace api.Infrastructure.Auth;

public sealed class AdminAccountProvisioner(AppDbContext db, IPasswordHasher<User> hasher)
{
    /// <summary>Provisions administrator credentials through trusted local administration, not an HTTP endpoint.</summary>
    public async Task ProvisionAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        if (username.Length > 200 || password.Length is < 12 or > 1024)
        {
            throw new ArgumentException("Use a username of at most 200 characters and a password of 12–1024 characters.");
        }

        var user = await db.Users.SingleOrDefaultAsync(user => user.Username == username, cancellationToken);
        if (user is null)
        {
            user = new User { Username = username, DisplayName = username };
            db.Users.Add(user);
        }

        user.IsAdmin = true;
        user.PasswordHash = hasher.HashPassword(user, password);
        await db.SaveChangesAsync(cancellationToken);
    }
}
