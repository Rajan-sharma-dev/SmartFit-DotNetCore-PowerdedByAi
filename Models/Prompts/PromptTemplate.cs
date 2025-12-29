namespace MiddleWareWebApi.Models.Prompts
{
    /// <summary>
    /// Represents a structured prompt template with system and user components
    /// </summary>
    public class PromptTemplate
    {
        /// <summary>
        /// System message that defines the AI's role, context, and behavior
        /// </summary>
        public string SystemPrompt { get; set; } = string.Empty;

        /// <summary>
        /// User prompt template that can contain placeholders for dynamic content
        /// </summary>
        public string UserPrompt { get; set; } = string.Empty;

        /// <summary>
        /// Version identifier for this prompt template (e.g., "v1", "v2")
        /// </summary>
        public string Version { get; set; } = string.Empty;

        /// <summary>
        /// Command intent this template is designed for (e.g., "CreateTask", "ListTasks")
        /// </summary>
        public string Intent { get; set; } = string.Empty;

        /// <summary>
        /// Optional metadata about the prompt template
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new();

        /// <summary>
        /// Replaces placeholders in the user prompt with actual values
        /// </summary>
        /// <param name="placeholders">Dictionary of placeholder names and their replacement values</param>
        /// <returns>User prompt with placeholders replaced</returns>
        public string BuildUserPrompt(Dictionary<string, string> placeholders)
        {
            var result = UserPrompt;
            
            foreach (var placeholder in placeholders)
            {
                var key = $"{{{{{placeholder.Key}}}}}"; // {{PLACEHOLDER_NAME}}
                result = result.Replace(key, placeholder.Value);
            }
            
            return result;
        }

        /// <summary>
        /// Creates a complete prompt template instance
        /// </summary>
        public static PromptTemplate Create(string intent, string version, string systemPrompt, string userPrompt)
        {
            return new PromptTemplate
            {
                Intent = intent,
                Version = version,
                SystemPrompt = systemPrompt,
                UserPrompt = userPrompt,
                Metadata = new Dictionary<string, string>
                {
                    ["CreatedAt"] = DateTime.UtcNow.ToString("O"),
                    ["Source"] = "FileSystem"
                }
            };
        }
    }
}