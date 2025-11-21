namespace MiddleWareWebApi.MiddleWare
{
    // C#
    public class JwtAuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly string _jwtSecret;

        public JwtAuthenticationMiddleware(RequestDelegate next, IConfiguration config)
        {
            _next = next;
            _jwtSecret = config["Jwt:Secret"];
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (token != null)
            {
                var principal = JwtUtils.ValidateToken(token, _jwtSecret);
                if (principal != null)
                    context.User = principal;
            }
            await _next(context);
        }

        public static class JwtUtils
        {
            public static System.Security.Claims.ClaimsPrincipal? ValidateToken(string token, string secret)
            {
                // TODO: Implement JWT validation logic here.
                // For now, return null to indicate invalid token.
                if (string.IsNullOrEmpty(token))
                {
                    return null;
                }
                // In a real implementation, validate the token and return the ClaimsPrincipal.
                return new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
                {
                    new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "TestUser")
                }, "Jwt"));
            }
        }
    }

}
