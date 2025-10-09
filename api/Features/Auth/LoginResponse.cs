using System;
using api.Features.Users.Dtos;

namespace api.Features.Auth;

public sealed record LoginResponse(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    UserResponse User);
