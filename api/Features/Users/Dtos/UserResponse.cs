namespace api.Features.Users.Dtos;

public sealed record UserResponse(
    int Id,
    string Username,
    string DisplayName,
    bool IsAdmin);
