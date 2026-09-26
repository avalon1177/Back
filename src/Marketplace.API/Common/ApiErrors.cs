namespace Marketplace.API.Common;

public static class ApiErrors
{
    public static object Problem(string message, int status = 400, string? code = null) =>
        new { error = new { message, code }, status };
}
