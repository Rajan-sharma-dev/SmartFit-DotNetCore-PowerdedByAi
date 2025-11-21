using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Azure;
using Newtonsoft.Json.Linq;
using System.Security.Claims;

namespace MiddleWareWebApi.MiddleWare
{
    public class DynamicServiceMiddleWare
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<DynamicServiceMiddleWare> _logger;

        public DynamicServiceMiddleWare(RequestDelegate next, ILogger<DynamicServiceMiddleWare> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            // Principal is automatically available in context.User after authentication
            // No need to manually extract or pass user details - services get them automatically
            var segments = context.Request.Path.Value?.Split('/');
            var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var jsonBody = await reader.ReadToEndAsync();
            
            if (segments != null && segments.Length > 4)
            {
                var serviceName = segments[4];
                var method = segments[5];

                // Log the service call with user info (if authenticated)
                var userId = context.User.Identity?.IsAuthenticated == true
                    ? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "Unknown"
                    : "Anonymous";

                _logger.LogInformation("Calling service: {ServiceName}.{Method} for user: {UserId}", 
                    serviceName, method, userId);

                var serviceType = Type.GetType($"MiddleWareWebApi.Services.{serviceName}");
                if (serviceType != null)
                {
                    var service = serviceProvider.GetService(serviceType);
                    if (service != null)
                    {
                        var methodInfo = service.GetType().GetMethod(method);
                        var parameters = methodInfo?.GetParameters();

                        var args = new object[parameters?.Length ?? 0];

                        // Process method parameters from request body
                        if (parameters != null && parameters.Length > 0)
                        {
                            for (int i = 0; i < parameters.Length; i++)
                            {
                                var nameValue = (object?)null;
                                var param = parameters[i];

                                // Skip parameters that are dependency injected (like ICurrentUserService)
                                // These will be handled by the DI container automatically
                                if (IsServiceParameter(param.ParameterType))
                                {
                                    continue; // Let DI handle this parameter
                                }

                                if (!string.IsNullOrEmpty(jsonBody) && jsonBody != "{}")
                                {
                                    if (!JObject.Parse(jsonBody).TryGetValue(
                                        param.Name, 
                                        StringComparison.OrdinalIgnoreCase,
                                        out var token))
                                    {
                                        if (!param.HasDefaultValue)
                                        {
                                            context.Response.StatusCode = 400;
                                            await context.Response.WriteAsJsonAsync(new
                                            {
                                                error = $"Missing required parameter '{param.Name}' in request body."
                                            });
                                            return;
                                        }
                                        nameValue = param.DefaultValue;
                                    }
                                    else
                                    {
                                        if (param.ParameterType.IsClass && param.ParameterType != typeof(string))
                                        {
                                            // Deserialize DTO
                                            var obj = JObject.Parse(jsonBody);
                                            nameValue = obj[param.Name]?.ToObject(param.ParameterType);

                                            // Validate DTO fields
                                            if (nameValue != null)
                                            {
                                                var validationContext = new ValidationContext(nameValue, null, null);
                                                var results = new List<ValidationResult>();
                                                bool isValid = Validator.TryValidateObject(nameValue, validationContext, results, true);

                                                if (!isValid)
                                                {
                                                    context.Response.StatusCode = 400;
                                                    await context.Response.WriteAsJsonAsync(new
                                                    {
                                                        error = $"Validation failed for parameter '{param.Name}'",
                                                        details = results.Select(r => r.ErrorMessage)
                                                    });
                                                    return;
                                                }
                                            }
                                        }
                                        else
                                        {
                                            // Primitive param
                                            var obj = JObject.Parse(jsonBody);
                                            nameValue = obj[param.Name]?.ToObject(param.ParameterType);
                                        }
                                    }
                                }
                                else if (!param.HasDefaultValue)
                                {
                                    context.Response.StatusCode = 400;
                                    await context.Response.WriteAsJsonAsync(new
                                    {
                                        error = $"Missing required parameter '{param.Name}'"
                                    });
                                    return;
                                }

                                args[i] = nameValue;
                            }
                        }

                        if (methodInfo != null)
                        {
                            try
                            {
                                // Services automatically have access to Principal via ICurrentUserService
                                // No need to manually pass user context
                                var result = args.Length == 0 
                                    ? methodInfo.Invoke(service, null)
                                    : methodInfo.Invoke(service, args);
                                
                                context.Items["ResponseData"] = result;

                                _logger.LogInformation("Service call completed successfully: {ServiceName}.{Method} for user: {UserId}", 
                                    serviceName, method, userId);
                            }
                            catch (System.Reflection.TargetInvocationException tex) when (tex.InnerException != null)
                            {
                                // Unwrap the inner exception for better error handling
                                var innerException = tex.InnerException;
                                
                                if (innerException is UnauthorizedAccessException)
                                {
                                    context.Response.StatusCode = 403; // Forbidden
                                    await context.Response.WriteAsJsonAsync(new
                                    {
                                        error = "Access denied",
                                        message = innerException.Message
                                    });
                                    return;
                                }

                                _logger.LogError(innerException, "Error invoking service method: {ServiceName}.{Method}", serviceName, method);
                                context.Response.StatusCode = 500;
                                await context.Response.WriteAsJsonAsync(new
                                {
                                    error = "An error occurred while processing your request",
                                    details = innerException.Message
                                });
                                return;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error invoking service method: {ServiceName}.{Method}", serviceName, method);
                                context.Response.StatusCode = 500;
                                await context.Response.WriteAsJsonAsync(new
                                {
                                    error = "An error occurred while processing your request",
                                    details = ex.Message
                                });
                                return;
                            }
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Service not found: {ServiceName}", serviceName);
                        context.Response.StatusCode = 404;
                        await context.Response.WriteAsJsonAsync(new
                        {
                            error = $"Service '{serviceName}' not found"
                        });
                        return;
                    }
                }
                else
                {
                    _logger.LogWarning("Service type not found: {ServiceName}", serviceName);
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsJsonAsync(new
                    {
                        error = $"Service type '{serviceName}' not found"
                    });
                    context.Response.StatusCode = 404;
                    return;
                }
            }

            await _next(context);
        }

        // Helper method to identify service parameters that should be dependency injected
        private static bool IsServiceParameter(Type parameterType)
        {
            // Check if it's a service interface or service class
            return parameterType.Name.StartsWith("I") && parameterType.IsInterface ||
                   parameterType.Name.EndsWith("Service") ||
                   parameterType == typeof(IServiceProvider) ||
                   parameterType == typeof(ILogger<>) ||
                   parameterType.IsGenericType && parameterType.GetGenericTypeDefinition() == typeof(ILogger<>);
        }
    }

    // Keep UserContext for backward compatibility, but it's no longer needed
    // Services now use ICurrentUserService which accesses Principal directly
    public class UserContext
    {
        public string? UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsAuthenticated { get; set; }
        public Dictionary<string, string> Claims { get; set; } = new();

        public bool IsInRole(string role) => Role.Equals(role, StringComparison.OrdinalIgnoreCase);
        public bool HasClaim(string claimType) => Claims.ContainsKey(claimType);
        public string? GetClaimValue(string claimType) => Claims.TryGetValue(claimType, out var value) ? value : null;
    }
}
