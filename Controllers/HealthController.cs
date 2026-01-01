using Microsoft.AspNetCore.Mvc;
using MiddleWareWebApi.Services.Interfaces;
using System.Reflection;
using MiddleWareWebApi.data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace MiddleWareWebApi.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly ILogger<HealthController> _logger;
        private readonly IOpenAiService _openAiService;
        private readonly DapperContext _dapperContext;

        public HealthController(
            ILogger<HealthController> logger, 
            IOpenAiService openAiService,
            DapperContext dapperContext)
        {
            _logger = logger;
            _openAiService = openAiService;
            _dapperContext = dapperContext;
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var health = new
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = GetVersion(),
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"dd\.hh\:mm\:ss"),
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = GC.GetTotalMemory(false),
                Checks = new
                {
                    Database = await CheckDatabaseAsync(),
                    OpenAI = await CheckOpenAIAsync(),
                    FileSystem = CheckFileSystem(),
                    Memory = CheckMemory()
                }
            };

            var hasUnhealthyChecks = HasUnhealthyChecks(health.Checks);
            var statusCode = hasUnhealthyChecks ? 503 : 200;

            _logger.LogInformation("Health check performed: {Status} - {StatusCode}", 
                hasUnhealthyChecks ? "Unhealthy" : "Healthy", statusCode);

            return StatusCode(statusCode, health);
        }

        [HttpGet("ready")]
        public async Task<IActionResult> Ready()
        {
            // Readiness check - can the app serve requests?
            var ready = new
            {
                Status = "Ready",
                Timestamp = DateTime.UtcNow,
                Version = GetVersion(),
                Dependencies = new
                {
                    Database = await CheckDatabaseAsync(),
                    OpenAI = await CheckOpenAIAsync()
                }
            };

            var isReady = IsSystemReady(ready.Dependencies);
            var statusCode = isReady ? 200 : 503;

            return StatusCode(statusCode, ready);
        }

        [HttpGet("live")]
        public IActionResult Live()
        {
            // Liveness check - is the app running?
            return Ok(new 
            { 
                Status = "Live", 
                Timestamp = DateTime.UtcNow,
                Version = GetVersion(),
                ProcessId = Environment.ProcessId
            });
        }

        [HttpGet("version")]
        public IActionResult Version()
        {
            var version = new
            {
                ApplicationVersion = GetVersion(),
                FrameworkVersion = Environment.Version.ToString(),
                OperatingSystem = Environment.OSVersion.ToString(),
                MachineName = Environment.MachineName,
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                BuildTimestamp = GetBuildTimestamp()
            };

            return Ok(version);
        }

        private async Task<object> CheckDatabaseAsync()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                
                using var connection = _dapperContext.CreateConnection();
                if (connection is SqlConnection sqlConnection)
                {
                    await sqlConnection.OpenAsync();
                }
                else
                {
                    connection.Open();
                }
                
                // Simple query to test database connectivity
                var result = await connection.QueryFirstOrDefaultAsync<int>("SELECT 1");
                
                var responseTime = DateTime.UtcNow - startTime;
                
                var isHealthy = result == 1;
                
                return new 
                { 
                    Status = isHealthy ? "Healthy" : "Unhealthy", 
                    ResponseTime = $"{responseTime.TotalMilliseconds:F0}ms",
                    DatabaseType = "SQL Server"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database health check failed");
                return new 
                { 
                    Status = "Unhealthy", 
                    Error = ex.Message,
                    ResponseTime = "N/A"
                };
            }
        }

        private async Task<object> CheckOpenAIAsync()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var isHealthy = await _openAiService.TestConnectionAsync();
                var responseTime = DateTime.UtcNow - startTime;
                
                return new 
                { 
                    Status = isHealthy ? "Healthy" : "Unhealthy",
                    ResponseTime = $"{responseTime.TotalMilliseconds:F0}ms"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OpenAI health check failed");
                return new 
                { 
                    Status = "Unhealthy", 
                    Error = ex.Message,
                    ResponseTime = "N/A"
                };
            }
        }

        private object CheckFileSystem()
        {
            try
            {
                var currentDirectory = Directory.GetCurrentDirectory();
                var tempPath = Path.GetTempPath();
                
                // Test write access
                var testFile = Path.Combine(tempPath, $"healthcheck_{Guid.NewGuid()}.tmp");
                System.IO.File.WriteAllText(testFile, "health check");
                System.IO.File.Delete(testFile);
                
                var drives = DriveInfo.GetDrives()
                    .Where(d => d.IsReady)
                    .Select(d => new 
                    {
                        Name = d.Name,
                        AvailableSpace = FormatBytes(d.AvailableFreeSpace),
                        TotalSpace = FormatBytes(d.TotalSize)
                    });

                return new
                {
                    Status = "Healthy",
                    CurrentDirectory = currentDirectory,
                    TempPath = tempPath,
                    Drives = drives
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "File system health check failed");
                return new
                {
                    Status = "Unhealthy",
                    Error = ex.Message
                };
            }
        }

        private object CheckMemory()
        {
            try
            {
                var workingSet = Environment.WorkingSet;
                var totalMemory = GC.GetTotalMemory(false);
                
                return new
                {
                    Status = "Healthy",
                    WorkingSet = FormatBytes(workingSet),
                    ManagedMemory = FormatBytes(totalMemory),
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2)
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Memory health check failed");
                return new
                {
                    Status = "Unhealthy",
                    Error = ex.Message
                };
            }
        }

        private static string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "Unknown";
        }

        private static string GetBuildTimestamp()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var buildDateTime = System.IO.File.GetCreationTime(assembly.Location);
            return buildDateTime.ToString("yyyy-MM-dd HH:mm:ss UTC");
        }

        private static string MaskConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return "Not configured";

            // Mask sensitive information in connection string
            var masked = connectionString;
            
            // Mask password
            masked = System.Text.RegularExpressions.Regex.Replace(
                masked, 
                @"Password=([^;]*)", 
                "Password=***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                
            // Mask user ID if present
            masked = System.Text.RegularExpressions.Regex.Replace(
                masked, 
                @"User Id=([^;]*)", 
                "User Id=***", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return masked;
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            
            return $"{number:n1} {suffixes[counter]}";
        }

        private static bool HasUnhealthyChecks(object checks)
        {
            var type = checks.GetType();
            var properties = type.GetProperties();
            
            foreach (var property in properties)
            {
                var value = property.GetValue(checks);
                if (value != null)
                {
                    var statusProperty = value.GetType().GetProperty("Status");
                    if (statusProperty != null)
                    {
                        var status = statusProperty.GetValue(value)?.ToString();
                        if (status == "Unhealthy")
                            return true;
                    }
                }
            }
            
            return false;
        }

        private static bool IsSystemReady(object dependencies)
        {
            return !HasUnhealthyChecks(dependencies);
        }
    }
}