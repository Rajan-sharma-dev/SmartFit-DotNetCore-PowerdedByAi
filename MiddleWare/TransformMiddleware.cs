namespace MiddleWareWebApi.MiddleWare
{
    // C#
    public class TransformMiddleware
    {
        private readonly RequestDelegate _next;
        public TransformMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context)
        {
            // Example: Add custom header
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.Add("X-Custom-Header", "Value");
                return Task.CompletedTask;
            });
            await _next(context);
        }
    }

}
