namespace api.Features.Users.Dtos;

public sealed record UpdateUserRequest(
    string Username,
    string DisplayName,
    bool IsAdmin);
