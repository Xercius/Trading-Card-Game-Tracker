using api.Authentication;
using api.Models;
using Microsoft.Extensions.Options;
using Xunit;

namespace api.Tests.Authentication;

public sealed class JwtTokenServiceTests
{
    [Fact]
    public void CreateToken_WithMissingUsername_ThrowsInvalidOperation()
    {
        var options = Options.Create(new JwtOptions
        {
            Key = new string('x', 32),
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenLifetimeMinutes = 5
        });

        var service = new JwtTokenService(options);

        var user = new User
        {
            Id = 1,
            Username = null!,
            DisplayName = "Tester",
            IsAdmin = false
        };

        var exception = Assert.Throws<InvalidOperationException>(() => service.CreateToken(user));
        Assert.Equal("Username required to create token", exception.Message);
    }
}
