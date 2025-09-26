namespace api.Features.Users.Dtos;

public sealed record CreateUserRequest(
    string Username,
    string DisplayName,
    bool IsAdmin);
