using api.Common.Errors;
using api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api.Features.Admin;

internal static class AdminControllerExtensions
{
    public static IActionResult LastAdminConflict(this ControllerBase controller)
    {
        return controller.CreateProblem(
            StatusCodes.Status409Conflict,
            title: "Cannot remove last administrator",
            detail: "At least one administrator must remain.");
    }

    public static async Task<IActionResult?> EnsureAnotherAdminRemainsAsync(
        this ControllerBase controller,
        AppDbContext db,
        bool removingAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!removingAdmin)
        {
            return null;
        }

        var adminCount = await db.Users.CountAsync(u => u.IsAdmin, cancellationToken);
        if (adminCount <= 1)
        {
            return controller.LastAdminConflict();
        }

        return null;
    }
}
