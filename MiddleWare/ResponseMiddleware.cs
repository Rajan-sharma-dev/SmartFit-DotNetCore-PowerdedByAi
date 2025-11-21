using System.Text.Json;
namespace MiddleWareWebApi.MiddleWare
{
    public class ResponseMiddleware
    {
        private readonly RequestDelegate _next;

        public ResponseMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (!context.Items.TryGetValue("ResponseData", out var raw))
                return;

            object result = raw;

            if (result is Task task)
            {
                await task;
                var taskType = task.GetType();
                if (taskType.IsGenericType)
                {
                    var resultProperty = taskType.GetProperty("Result");
                    result = resultProperty.GetValue(task);
                }
                else
                {
                    result = null; // void Task
                }
            }

            switch (result)
            {
                case null:
                    context.Response.StatusCode = 204;
                    break;

                case string str:
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync(str);
                    break;

                case byte[] bytes:
                    context.Response.ContentType = "application/octet-stream";
                    await context.Response.Body.WriteAsync(bytes, 0, bytes.Length);
                    break;

                case Stream stream:
                    context.Response.ContentType = "application/octet-stream";
                    await stream.CopyToAsync(context.Response.Body);
                    break;

                default:
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(JsonSerializer.Serialize(result));
                    break;
            }
        }
    }

}
