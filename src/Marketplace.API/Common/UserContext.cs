using System.Security.Claims;

namespace Marketplace.API.Common;

public static class UserContext
{
    public static Guid RequireUserId(this ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(ClaimTypes.Name) ?? user.FindFirstValue("sub");
        if (id is null) throw new InvalidOperationException("User id not found in token");
        return Guid.Parse(id);
    }
}
