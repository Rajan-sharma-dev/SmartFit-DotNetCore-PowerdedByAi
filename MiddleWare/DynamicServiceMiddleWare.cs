using System.ComponentModel.DataAnnotations;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using Azure;
using Newtonsoft.Json.Linq;

namespace MiddleWareWebApi.MiddleWare
{
    public class DynamicServiceMiddleWare
    {
        private readonly RequestDelegate _next;

        public DynamicServiceMiddleWare(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, IServiceProvider serviceProvider)
        {
            // Logic to dynamically add services can be implemented here
            // For example, based on request headers or other criteria
            var segmants = context.Request.Path.Value?.Split('/');
            var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            var jsonBody = await reader.ReadToEndAsync();
            
            if (segmants != null && segmants.Length > 1)
            {
                var serviceName = segmants[4];
                var method = segmants[5];
                // Assuming the service name is in the first segment of the path
                // method is in the second segment of the path

                var serviceType = Type.GetType($"MiddleWareWebApi.Services.{serviceName}");
                if (serviceType != null)
                {
                    var service = serviceProvider.GetService(serviceType);
                    if (service != null)
                    {
                        var methodInfo = service.GetType().GetMethod(method);
                        var parameters = methodInfo?.GetParameters();


                        var args = new object[parameters.Length];

                        // Read and deserialize the request body if there are parameter
                        // for mulitple parameters you need to adjust this logi
                        if (parameters.Length > 0)
                        {
                            for (int i = 0; i < parameters.Length; i++)
                            {
                                var nameValue = (object)null;
                                var param = parameters[i];

                                if (!JObject.Parse(jsonBody).TryGetValue(
                                    param.Name, 
                                    StringComparison.OrdinalIgnoreCase,
                                    out var token))
                                {
                                    context.Response.StatusCode = 400;
                                    await context.Response.WriteAsJsonAsync(new
                                    {
                                        error = $"Missing required parameter '{param.Name}' in request body."
                                    });
                                    return;
                                }
                                JObject obj = null;

                                if (param.ParameterType.IsClass && param.ParameterType != typeof(string))
                                {
                                    // Deserialize DTO
                                    obj = JObject.Parse(jsonBody.ToString());
                                    nameValue = obj[param.Name]?.ToObject(param.ParameterType);

                                    // Validate DTO fields
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
                                        return; // stop request processing
                                    }
                                }
                                else
                                {
                                    // Primitive param
                                    obj = JObject.Parse(jsonBody.ToString());
                                    nameValue = obj[param.Name]?.ToObject(param.ParameterType);
                                    if (obj == null && param.HasDefaultValue == false)
                                    {
                                        context.Response.StatusCode = 400;
                                        await context.Response.WriteAsJsonAsync(new
                                        {
                                            error = $"Missing required primitive parameter '{param.Name}'"
                                        });
                                        return;
                                    }
                                }

                                args[i] = nameValue;
                            }

                        }



                        if (methodInfo != null)
                        {
                            // Invoke the method (assuming it has no parameters for simplicity)
                            var paramValues = new object() { };
                            // Example for a method with one string parameter
                      
                           // var name = methodInfo.GetParameters().First().Name;
                           var result = new object();
                           var arg = new object();
                           if (args.Length == 0)
                            {
                                result = methodInfo.Invoke(service, null);
                                context.Items["ResponseData"] = result;

                            } 
                            else if (args.Length > 0)
                            {
                                // arg = args.First();
                                result = methodInfo.Invoke(service, args);
                                context.Items["ResponseData"] = result;
                            }
                        }

                    }
                }
            }

            await _next(context);

        }
    }
}
