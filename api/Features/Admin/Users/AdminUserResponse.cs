namespace api.Features.Admin.Users;

public sealed record AdminUserResponse(
    int Id,
    string Name,
    string Username,
    string DisplayName,
    bool IsAdmin,
    DateTime CreatedUtc);
