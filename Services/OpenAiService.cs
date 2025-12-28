using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;
using MiddleWareWebApi.Models.Configuration;
using MiddleWareWebApi.Services.Interfaces;
using System.Net.Http.Headers;

namespace MiddleWareWebApi.Services
{
    /// <summary>
    /// Basic OpenAI service for general AI functionality
    /// </summary>
    public class OpenAiService : IOpenAiService
    {
        private readonly HttpClient _httpClient;
        private readonly OpenAiSettings _settings;
        private readonly ILogger<OpenAiService> _logger;

        public OpenAiService(
            HttpClient httpClient,
            IOptions<OpenAiSettings> settings,
            ILogger<OpenAiService> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.ApiKey);
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ProjectManagement-Basic-AI/1.0");
            _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);
        }

        public async Task<string> GetChatCompletionAsync(string prompt, string? systemMessage = null)
        {
            try
            {
                var messages = new List<object>();
                
                if (!string.IsNullOrEmpty(systemMessage))
                {
                    messages.Add(new { role = "system", content = systemMessage });
                }
                
                messages.Add(new { role = "user", content = prompt });

                var requestBody = new
                {
                    model = _settings.DefaultModel,
                    messages = messages,
                    max_tokens = _settings.MaxTokens,
                    temperature = _settings.Temperature,
                    top_p = _settings.TopP
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogDebug("Sending basic AI request: {Model}", _settings.DefaultModel);

                var response = await _httpClient.PostAsync("/chat/completions", content);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("OpenAI API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                    throw new HttpRequestException($"OpenAI API error: {response.StatusCode}");
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OpenAiChatResponse>(responseContent);

                var assistantResponse = result?.Choices?.FirstOrDefault()?.Message?.Content ?? "I apologize, but I couldn't generate a response at this time.";
                
                _logger.LogInformation("Basic AI response received successfully. Tokens used: {Tokens}", 
                    result?.Usage?.TotalTokens ?? 0);

                return assistantResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetChatCompletionAsync");
                throw;
            }
        }

        public async Task<string> GenerateTextAsync(string prompt)
        {
            var systemMessage = "You are a helpful AI assistant specialized in project management and software development.";
            return await GetChatCompletionAsync(prompt, systemMessage);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await GetChatCompletionAsync(
                    "Hello, this is a connection test.", 
                    "Reply with exactly 'Connection test successful'");
                
                var isSuccessful = response.Contains("Connection test successful");
                
                _logger.LogInformation("Basic AI connection test result: {Success}", isSuccessful);
                return isSuccessful;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Basic AI connection test failed");
                return false;
            }
        }

        public async Task<string> SummarizeTextAsync(string text, int maxLength = 150)
        {
            var prompt = $@"Summarize the following text in approximately {maxLength} words:
                          {text}";

            return await GetChatCompletionAsync(prompt, "You are a text summarization assistant. Create concise, accurate summaries.");
        }

        public async Task<string> TranslateTextAsync(string text, string targetLanguage)
        {
            var prompt = $@"Translate the following text to {targetLanguage}:
                          {text}";

            return await GetChatCompletionAsync(prompt, $"You are a professional translator. Translate accurately to {targetLanguage}.");
        }

        public async Task<bool> ModerateContentAsync(string content)
        {
            if (!_settings.EnableModeration)
                return true;

            try
            {
                var requestBody = new { input = content };
                var json = JsonSerializer.Serialize(requestBody);
                var requestContent = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("/moderations", requestContent);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Moderation API failed, allowing content by default");
                    return true;
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<OpenAiModerationResponse>(responseContent);

                var isFlagged = result?.Results?.Any(r => r.Flagged) ?? false;
                
                if (isFlagged)
                {
                    _logger.LogWarning("Content flagged by moderation: {ContentPreview}", 
                        content.Length > 100 ? content.Substring(0, 100) + "..." : content);
                }

                return !isFlagged;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in content moderation, allowing content by default");
                return true; // Allow content if moderation fails
            }
        }

        // Response Models for JSON deserialization
        private class OpenAiChatResponse
        {
            public List<Choice>? Choices { get; set; }
            public Usage? Usage { get; set; }
        }

        private class Choice
        {
            public Message? Message { get; set; }
        }

        private class Message
        {
            public string Content { get; set; } = string.Empty;
        }

        private class Usage
        {
            public int TotalTokens { get; set; }
        }

        private class OpenAiModerationResponse
        {
            public List<ModerationResult>? Results { get; set; }
        }

        private class ModerationResult
        {
            public bool Flagged { get; set; }
        }
    }
}