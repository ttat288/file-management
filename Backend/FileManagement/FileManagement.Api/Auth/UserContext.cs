using System.Security.Claims;

namespace FileManagement.Api.Auth
{
    public static class UserContext
    {
        public static Guid GetUserId(ClaimsPrincipal user)
        {
            var id = user.FindFirstValue("uid") ?? user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
            if (id == null) throw new InvalidOperationException("Missing user id claim");
            return Guid.Parse(id);
        }
    }
}

