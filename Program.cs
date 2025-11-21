// C#
using MiddleWareWebApi;
using MiddleWareWebApi.data;
using MiddleWareWebApi.MiddleWare;
using MiddleWareWebApi.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddSingleton<DapperContext>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TaskService>();

var app = builder.Build();

builder.Services.AddApplicationServices();

app.UseMiddleware<ErrorHandlingMiddleware>();
app.UseMiddleware<LoggingMiddleware>();
app.UseMiddleware<JwtAuthenticationMiddleware>();
app.UseMiddleware<TransformMiddleware>();
app.UseMiddleware<DynamicServiceMiddleWare>();
app.UseMiddleware<ResponseMiddleware>();

app.MapControllers();
app.Run();
