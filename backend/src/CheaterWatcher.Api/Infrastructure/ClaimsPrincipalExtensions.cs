using System.Security.Claims;

namespace CheaterWatcher.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Returns the authenticated user's id from the JWT, or null for anonymous requests.
    /// </summary>
    public static int? TryGetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? user.FindFirstValue("sub");
        return int.TryParse(value, out var id) ? id : null;
    }
}
