using MiddleWareWebApi.Models.Identity;

namespace MiddleWareWebApi.Services.Interfaces
{
    /// <summary>
    /// Basic OpenAI service interface for general AI functionality
    /// </summary>
    public interface IOpenAiService
    {
        // Core Chat Methods
        Task<string> GetChatCompletionAsync(string prompt, string? systemMessage = null);
        Task<string> GenerateTextAsync(string prompt);
        
        // Utility Methods
        Task<bool> TestConnectionAsync();
        Task<string> SummarizeTextAsync(string text, int maxLength = 150);
        Task<string> TranslateTextAsync(string text, string targetLanguage);
        Task<bool> ModerateContentAsync(string content);
    }
}