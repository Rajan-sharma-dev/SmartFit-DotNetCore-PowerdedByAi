using MiddleWareWebApi.Services.Interfaces;
using MiddleWareWebApi.Services;

namespace MiddleWareWebApi.Extensions
{
    /// <summary>
    /// Extension methods for registering prompt management services
    /// </summary>
    public static class PromptServiceExtensions
    {
        /// <summary>
        /// Registers prompt management services with dependency injection container
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <param name="configuration">Application configuration</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddPromptManagement(this IServiceCollection services, IConfiguration configuration)
        {
            // Register prompt provider (file-based implementation)
            services.AddSingleton<IPromptProvider, FilePromptProvider>();
            
            // Register prompt orchestration service
            services.AddScoped<IPromptOrchestrationService, PromptOrchestrationService>();
            
            // Register HTTP client for AI service calls if not already registered
            if (!services.Any(x => x.ServiceType == typeof(HttpClient)))
            {
                services.AddHttpClient();
            }
            
            return services;
        }

        /// <summary>
        /// Registers AI command services with the new prompt management architecture
        /// </summary>
        /// <param name="services">Service collection</param>
        /// <returns>Service collection for chaining</returns>
        public static IServiceCollection AddAiCommandServices(this IServiceCollection services)
        {
            // Register the updated AI Command Interpreter
            services.AddScoped<IAiCommandInterpreter, AiCommandInterpreter>();
            
            return services;
        }

        /// <summary>
        /// Configures prompt management with validation and logging
        /// </summary>
        /// <param name="app">Application builder</param>
        /// <param name="configuration">Application configuration</param>
        /// <returns>Application builder for chaining</returns>
        public static IApplicationBuilder UsePromptManagement(this IApplicationBuilder app, IConfiguration configuration)
        {
            // Validate prompt directory exists
            var promptsPath = configuration.GetValue<string>("PromptsPath") ?? 
                              Path.Combine(Directory.GetCurrentDirectory(), "Prompts");
            
            if (!Directory.Exists(promptsPath))
            {
                var logger = app.ApplicationServices.GetService<ILogger<FilePromptProvider>>();
                logger?.LogWarning("Prompts directory not found at: {Path}. Creating it now.", promptsPath);
                Directory.CreateDirectory(promptsPath);
            }
            
            // Log available prompts at startup
            using var scope = app.ApplicationServices.CreateScope();
            var promptProvider = scope.ServiceProvider.GetService<IPromptProvider>();
            
            Task.Run(async () =>
            {
                try
                {
                    var logger = app.ApplicationServices.GetService<ILogger<FilePromptProvider>>();
                    var intents = await promptProvider?.GetAvailableIntentsAsync()!;
                    logger?.LogInformation("Available prompt intents: {Intents}", string.Join(", ", intents));
                    
                    foreach (var intent in intents)
                    {
                        var versions = await promptProvider.GetAvailableVersionsAsync(intent);
                        logger?.LogDebug("Intent '{Intent}' has versions: {Versions}", intent, string.Join(", ", versions));
                    }
                }
                catch (Exception ex)
                {
                    var logger = app.ApplicationServices.GetService<ILogger<FilePromptProvider>>();
                    logger?.LogError(ex, "Error loading prompt information at startup");
                }
            });
            
            return app;
        }
    }
}