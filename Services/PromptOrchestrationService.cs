using MiddleWareWebApi.Models.Prompts;
using MiddleWareWebApi.Services.Interfaces;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace MiddleWareWebApi.Services
{
    /// <summary>
    /// Orchestrates AI prompts using the prompt provider abstraction.
    /// Separates prompt management from business logic and AI API calls.
    /// </summary>
    public class PromptOrchestrationService : IPromptOrchestrationService
    {
        private readonly IPromptProvider _promptProvider;
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PromptOrchestrationService> _logger;
        private static readonly Regex PlaceholderRegex = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

        public PromptOrchestrationService(
            IPromptProvider promptProvider,
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<PromptOrchestrationService> logger)
        {
            _promptProvider = promptProvider;
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            
            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            var openAiSettings = _configuration.GetSection("OpenAi");
            var apiKey = openAiSettings["ApiKey"];
            var baseUrl = openAiSettings["BaseUrl"];
            var timeoutSeconds = openAiSettings.GetValue<int>("TimeoutSeconds", 30);

            if (!string.IsNullOrEmpty(baseUrl))
            {
                _httpClient.BaseAddress = new Uri(baseUrl);
            }
            
            if (!string.IsNullOrEmpty(apiKey))
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "PromptOrchestration/1.0");
            }
            
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }

        public async Task<string> ExecutePromptAsync(string intent, Dictionary<string, string> placeholders, string? version = null)
        {
            try
            {
                var preparedPrompt = await GetPreparedPromptAsync(intent, placeholders, version);
                if (!preparedPrompt.HasValue)
                {
                    throw new InvalidOperationException($"Could not load prompt for intent: {intent}");
                }

                var (systemMessage, userMessage) = preparedPrompt.Value;
                return await CallAiServiceAsync(systemMessage, userMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing prompt for intent: {Intent}", intent);
                throw;
            }
        }

        public async Task<(string SystemMessage, string UserMessage)?> GetPreparedPromptAsync(
            string intent, 
            Dictionary<string, string> placeholders, 
            string? version = null)
        {
            try
            {
                // Get the prompt template
                var template = version != null 
                    ? await _promptProvider.GetPromptAsync(intent, version)
                    : await _promptProvider.GetLatestPromptAsync(intent);

                if (template is null)
                {
                    _logger.LogWarning("Prompt template not found for intent: {Intent}, version: {Version}", intent, version);
                    return null;
                }

                // Validate placeholders
                var missingPlaceholders = await ValidatePromptPlaceholdersAsync(intent, placeholders, version);
                if (missingPlaceholders.Any())
                {
                    _logger.LogWarning("Missing required placeholders for {Intent}: {Missing}", 
                        intent, string.Join(", ", missingPlaceholders));
                    // Continue anyway - some placeholders might be optional
                }

                // Replace placeholders in user prompt
                var userMessage = template.BuildUserPrompt(placeholders);
                
                return (template.SystemPrompt, userMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error preparing prompt for intent: {Intent}", intent);
                return null;
            }
        }

        public async Task<IEnumerable<string>> ValidatePromptPlaceholdersAsync(
            string intent, 
            Dictionary<string, string> placeholders, 
            string? version = null)
        {
            try
            {
                var template = version != null 
                    ? await _promptProvider.GetPromptAsync(intent, version)
                    : await _promptProvider.GetLatestPromptAsync(intent);

                if (template is null)
                {
                    return new[] { "Template not found" };
                }

                // Extract all placeholders from the user prompt template
                var matches = PlaceholderRegex.Matches(template.UserPrompt);
                var requiredPlaceholders = matches
                    .Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Distinct()
                    .ToList();

                // Find missing placeholders
                var providedKeys = placeholders?.Keys ?? Enumerable.Empty<string>();
                var missing = requiredPlaceholders.Except(providedKeys, StringComparer.OrdinalIgnoreCase);

                return missing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating placeholders for intent: {Intent}", intent);
                return new[] { "Validation error" };
            }
        }

        private async Task<string> CallAiServiceAsync(string systemMessage, string userMessage)
        {
            try
            {
                var openAiSettings = _configuration.GetSection("OpenAi");
                var model = openAiSettings["DefaultModel"] ?? "gpt-3.5-turbo";

                var requestBody = new
                {
                    model = model,
                    messages = new[]
                    {
                        new { role = "system", content = systemMessage },
                        new { role = "user", content = userMessage }
                    },
                    max_tokens = 800,
                    temperature = 0.3
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/v1/chat/completions", content);
                response.EnsureSuccessStatusCode();

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

                return result.GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling AI service");
                throw new InvalidOperationException("Failed to get AI response", ex);
            }
        }
    }
}