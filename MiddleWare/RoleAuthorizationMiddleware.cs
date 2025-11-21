namespace MiddleWareWebApi.MiddleWare
{
    // C#
    public class RoleAuthorizationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _requiredRole;

        public RoleAuthorizationMiddleware(RequestDelegate next, string requiredRole)
        {
            _next = next;
            _requiredRole = requiredRole;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.User.Identity.IsAuthenticated ||
                !context.User.IsInRole(_requiredRole))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden");
                return;
            }
            await _next(context);
        }
    }

}
