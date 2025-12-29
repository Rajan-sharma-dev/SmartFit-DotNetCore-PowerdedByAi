namespace MiddleWareWebApi.Services.Interfaces
{
    /// <summary>
    /// Service for orchestrating AI prompts using the prompt provider abstraction.
    /// Handles prompt retrieval, placeholder replacement, and AI API calls.
    /// </summary>
    public interface IPromptOrchestrationService
    {
        /// <summary>
        /// Executes a prompt for the given intent with placeholder values
        /// </summary>
        /// <param name="intent">Command intent (e.g., "CreateTask")</param>
        /// <param name="placeholders">Values to replace in prompt placeholders</param>
        /// <param name="version">Prompt version to use (defaults to latest)</param>
        /// <returns>AI response content</returns>
        Task<string> ExecutePromptAsync(string intent, Dictionary<string, string> placeholders, string? version = null);

        /// <summary>
        /// Gets a prepared prompt template with placeholders replaced
        /// </summary>
        /// <param name="intent">Command intent</param>
        /// <param name="placeholders">Values to replace in prompt placeholders</param>
        /// <param name="version">Prompt version to use (defaults to latest)</param>
        /// <returns>Prepared prompt with system and user messages</returns>
        Task<(string SystemMessage, string UserMessage)?> GetPreparedPromptAsync(string intent, Dictionary<string, string> placeholders, string? version = null);

        /// <summary>
        /// Validates that required placeholders are provided for a prompt
        /// </summary>
        /// <param name="intent">Command intent</param>
        /// <param name="placeholders">Provided placeholder values</param>
        /// <param name="version">Prompt version to check</param>
        /// <returns>List of missing required placeholders</returns>
        Task<IEnumerable<string>> ValidatePromptPlaceholdersAsync(string intent, Dictionary<string, string> placeholders, string? version = null);
    }
}