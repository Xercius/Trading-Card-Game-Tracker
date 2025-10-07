namespace api.Features.Admin.Users;

public sealed class AdminUpdateUserRequest
{
    public string? Name { get; init; }
    public bool? IsAdmin { get; init; }
}
