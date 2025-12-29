using MiddleWareWebApi.Models.Prompts;

namespace MiddleWareWebApi.Services.Interfaces
{
    /// <summary>
    /// Abstraction for providing prompt templates to the application layer.
    /// Implementations can load prompts from files, databases, APIs, etc.
    /// </summary>
    public interface IPromptProvider
    {
        /// <summary>
        /// Retrieves a prompt template by intent and version
        /// </summary>
        /// <param name="intent">Command intent (e.g., "CreateTask", "ListTasks")</param>
        /// <param name="version">Version identifier (e.g., "v1", "v2")</param>
        /// <returns>Prompt template if found, null otherwise</returns>
        Task<PromptTemplate?> GetPromptAsync(string intent, string version = "v1");

        /// <summary>
        /// Retrieves the latest version of a prompt template by intent
        /// </summary>
        /// <param name="intent">Command intent</param>
        /// <returns>Latest prompt template if found, null otherwise</returns>
        Task<PromptTemplate?> GetLatestPromptAsync(string intent);

        /// <summary>
        /// Gets all available versions for a specific intent
        /// </summary>
        /// <param name="intent">Command intent</param>
        /// <returns>List of available versions</returns>
        Task<IEnumerable<string>> GetAvailableVersionsAsync(string intent);

        /// <summary>
        /// Gets all available intents in the prompt provider
        /// </summary>
        /// <returns>List of available intents</returns>
        Task<IEnumerable<string>> GetAvailableIntentsAsync();

        /// <summary>
        /// Checks if a prompt template exists for the specified intent and version
        /// </summary>
        /// <param name="intent">Command intent</param>
        /// <param name="version">Version identifier</param>
        /// <returns>True if prompt exists, false otherwise</returns>
        Task<bool> PromptExistsAsync(string intent, string version = "v1");
    }
}