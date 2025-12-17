using System.Security.Claims;

namespace MiddleWareWebApi.Models.Identity
{
    /// <summary>
    /// DTO representing the current user's principal information
    /// Auto-injected by middleware into service methods that require it
    /// </summary>
    public class PrincipalDto
    {
        public int? UserId { get; set; }
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public string? FullName { get; set; }
        public bool IsAuthenticated { get; set; }
        public DateTime? LoginTime { get; set; }
        public string? IpAddress { get; set; }
    }
}