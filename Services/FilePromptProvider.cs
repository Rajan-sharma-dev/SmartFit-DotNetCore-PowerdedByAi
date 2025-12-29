using MiddleWareWebApi.Models.Prompts;
using MiddleWareWebApi.Services.Interfaces;
using System.Collections.Concurrent;

namespace MiddleWareWebApi.Services
{
    /// <summary>
    /// File-based implementation of IPromptProvider that loads prompts from the file system.
    /// Supports versioning and caching for performance.
    /// </summary>
    public class FilePromptProvider : IPromptProvider
    {
        private readonly string _promptsBasePath;
        private readonly ILogger<FilePromptProvider> _logger;
        private readonly ConcurrentDictionary<string, PromptTemplate> _promptCache;
        private readonly FileSystemWatcher _fileWatcher;

        public FilePromptProvider(ILogger<FilePromptProvider> logger, IConfiguration configuration)
        {
            _logger = logger;
            _promptCache = new ConcurrentDictionary<string, PromptTemplate>();
            
            // Get prompts path from configuration or use default
            _promptsBasePath = configuration.GetValue<string>("PromptsPath") ?? 
                               Path.Combine(Directory.GetCurrentDirectory(), "Prompts");
            
            // Ensure prompts directory exists
            if (!Directory.Exists(_promptsBasePath))
            {
                Directory.CreateDirectory(_promptsBasePath);
                _logger.LogWarning("Created prompts directory at: {Path}", _promptsBasePath);
            }

            // Set up file system watcher for cache invalidation
            _fileWatcher = new FileSystemWatcher(_promptsBasePath, "*.md")
            {
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };
            
            _fileWatcher.Changed += OnPromptFileChanged;
            _fileWatcher.Deleted += OnPromptFileChanged;
            _fileWatcher.Created += OnPromptFileChanged;
        }

        public async Task<PromptTemplate?> GetPromptAsync(string intent, string version = "v1")
        {
            if (string.IsNullOrWhiteSpace(intent))
                return null;

            var cacheKey = $"{intent}_{version}";
            
            // Check cache first
            if (_promptCache.TryGetValue(cacheKey, out var cachedPrompt))
            {
                return cachedPrompt;
            }

            // Load from file system
            try
            {
                var promptPath = Path.Combine(_promptsBasePath, version, intent);
                
                if (!Directory.Exists(promptPath))
                {
                    _logger.LogWarning("Prompt directory not found: {Path}", promptPath);
                    return null;
                }

                var systemFile = Path.Combine(promptPath, "system.md");
                var userFile = Path.Combine(promptPath, "user.md");

                if (!File.Exists(systemFile) || !File.Exists(userFile))
                {
                    _logger.LogWarning("Missing prompt files in: {Path}. Expected system.md and user.md", promptPath);
                    return null;
                }

                var systemPrompt = await File.ReadAllTextAsync(systemFile);
                var userPrompt = await File.ReadAllTextAsync(userFile);

                var template = PromptTemplate.Create(intent, version, systemPrompt, userPrompt);
                
                // Cache the loaded prompt
                _promptCache.TryAdd(cacheKey, template);
                
                _logger.LogDebug("Loaded prompt template: {Intent} v{Version}", intent, version);
                return template;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading prompt template: {Intent} v{Version}", intent, version);
                return null;
            }
        }

        public async Task<PromptTemplate?> GetLatestPromptAsync(string intent)
        {
            if (string.IsNullOrWhiteSpace(intent))
                return null;

            try
            {
                var versions = await GetAvailableVersionsAsync(intent);
                var latestVersion = versions
                    .OrderByDescending(v => v) // Simple string ordering, could be improved with semantic versioning
                    .FirstOrDefault();

                return latestVersion != null ? await GetPromptAsync(intent, latestVersion) : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest prompt for intent: {Intent}", intent);
                return null;
            }
        }

        public async Task<IEnumerable<string>> GetAvailableVersionsAsync(string intent)
        {
            if (string.IsNullOrWhiteSpace(intent))
                return Enumerable.Empty<string>();

            try
            {
                var versions = new List<string>();
                var versionDirs = Directory.GetDirectories(_promptsBasePath, "v*");

                foreach (var versionDir in versionDirs)
                {
                    var versionName = Path.GetFileName(versionDir);
                    var intentPath = Path.Combine(versionDir, intent);
                    
                    if (Directory.Exists(intentPath) && 
                        File.Exists(Path.Combine(intentPath, "system.md")) &&
                        File.Exists(Path.Combine(intentPath, "user.md")))
                    {
                        versions.Add(versionName);
                    }
                }

                return versions.OrderBy(v => v);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available versions for intent: {Intent}", intent);
                return Enumerable.Empty<string>();
            }
        }

        public async Task<IEnumerable<string>> GetAvailableIntentsAsync()
        {
            try
            {
                var intents = new HashSet<string>();
                var versionDirs = Directory.GetDirectories(_promptsBasePath, "v*");

                foreach (var versionDir in versionDirs)
                {
                    var intentDirs = Directory.GetDirectories(versionDir);
                    foreach (var intentDir in intentDirs)
                    {
                        var intentName = Path.GetFileName(intentDir);
                        if (File.Exists(Path.Combine(intentDir, "system.md")) &&
                            File.Exists(Path.Combine(intentDir, "user.md")))
                        {
                            intents.Add(intentName);
                        }
                    }
                }

                return intents.OrderBy(i => i);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available intents");
                return Enumerable.Empty<string>();
            }
        }

        public async Task<bool> PromptExistsAsync(string intent, string version = "v1")
        {
            var prompt = await GetPromptAsync(intent, version);
            return prompt != null;
        }

        private void OnPromptFileChanged(object sender, FileSystemEventArgs e)
        {
            // Invalidate cache when prompt files change
            try
            {
                var relativePath = Path.GetRelativePath(_promptsBasePath, e.FullPath);
                var pathParts = relativePath.Split(Path.DirectorySeparatorChar);
                
                if (pathParts.Length >= 2)
                {
                    var version = pathParts[0];
                    var intent = pathParts[1];
                    var cacheKey = $"{intent}_{version}";
                    
                    _promptCache.TryRemove(cacheKey, out _);
                    _logger.LogDebug("Invalidated cache for prompt: {Intent} v{Version}", intent, version);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error handling prompt file change: {Path}", e.FullPath);
            }
        }

        public void Dispose()
        {
            _fileWatcher?.Dispose();
        }
    }
}