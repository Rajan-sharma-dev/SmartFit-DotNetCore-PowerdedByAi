namespace MiddleWareWebApi.Models.Configuration
{
    public class OpenAiSettings
    {
        public string ApiKey { get; set; } = "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";
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        public string DefaultModel { get; set; } = "gpt-4o-mini";
        public string ImageModel { get; set; } = "gpt-4-vision-preview";
        public int MaxTokens { get; set; } = 1500;
        public double Temperature { get; set; } = 0.7;
        public double TopP { get; set; } = 1.0;
        public int TimeoutSeconds { get; set; } = 30;
        public bool EnableStreaming { get; set; } = true;
        public bool EnableModeration { get; set; } = true;
        
        // Rate Limiting
        public int RequestsPerMinute { get; set; } = 60;
        public int RequestsPerDay { get; set; } = 1000;
        
        // Project Management Specific Settings
        public bool EnablePersonalization { get; set; } = true;
        public bool EnableCodeAnalysis { get; set; } = true;
        public string DefaultProjectContext { get; set; } = "You are a senior project manager and technical lead specializing in software development project management, Agile methodologies, and team productivity.";
    }
}