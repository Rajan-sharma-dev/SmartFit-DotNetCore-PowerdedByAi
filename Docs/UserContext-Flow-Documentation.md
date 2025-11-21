# User Authentication Flow and Service Access

## How User Details Flow Through Your Application

### 1. Authentication Process
```
User Login ? JWT Token ? Token Validation ? User Claims ? Service Access
```

### 2. Middleware Pipeline Order
```
1. ErrorHandlingMiddleware
2. LoggingMiddleware  
3. JwtAuthenticationMiddleware ? Sets HttpContext.User & Items
4. Authentication & Authorization ? ASP.NET Core built-in
5. TransformMiddleware
6. DynamicServiceMiddleware ? Can access user details
7. ResponseMiddleware
```

### 3. How User Details Are Available

#### In JwtAuthenticationMiddleware
```csharp
// Extracts JWT token and sets user context
context.User = principal; // ClaimsPrincipal
context.Items["UserId"] = userId;
context.Items["Username"] = username;
context.Items["UserEmail"] = email;
context.Items["UserRole"] = role;
context.Items["IsAuthenticated"] = true;
```

#### In DynamicServiceMiddleware
```csharp
// Automatically injects UserContext into service methods
var currentUser = GetCurrentUserFromContext(context);

// Calls service methods with user context
methodInfo.Invoke(service, args); // args includes UserContext
```

#### In Your Services
```csharp
public async Task<SomeResult> MyServiceMethod(
    string someParam, 
    UserContext? userContext = null) // ? User details automatically injected
{
    if (userContext?.IsAuthenticated == true)
    {
        var userId = userContext.UserId;
        var userRole = userContext.Role;
        
        // Use user details for business logic
        if (!userContext.IsInRole("Admin"))
            throw new UnauthorizedAccessException();
    }
}
```

### 4. Ways to Access User Details in Services

#### Method 1: Through UserContext Parameter (Recommended)
```csharp
public async Task<IEnumerable<User>> GetAllUsersAsync(UserContext? userContext = null)
{
    if (userContext?.IsAuthenticated != true)
        throw new UnauthorizedAccessException();
        
    if (!userContext.IsInRole("Admin"))
        throw new UnauthorizedAccessException("Admin role required");
        
    // Service logic here
}
```

#### Method 2: Through HttpContext (In Controllers)
```csharp
[HttpGet]
[Authorize]
public async Task<IActionResult> GetUsers()
{
    var currentUser = HttpContext.GetCurrentUser();
    var users = await _userService.GetAllUsersAsync(currentUser);
    return Ok(users);
}
```

#### Method 3: Through ClaimsPrincipal (In Controllers)
```csharp
[HttpGet]
[Authorize]
public async Task<IActionResult> GetProfile()
{
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
    // Use user details
}
```

### 5. Dynamic Service Call Examples

#### Example 1: Service Call with Authentication
```http
POST /api/services/UserService/GetAllUsersAsync
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{}
```

**What happens:**
1. JWT token validated ? User claims extracted
2. UserContext created from claims  
3. GetAllUsersAsync(UserContext userContext) called
4. Service checks userContext.IsInRole("Admin")
5. Returns data if authorized

#### Example 2: Service Call with Parameters + User Context
```http
POST /api/services/UserService/UpdateUserAsync  
Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
Content-Type: application/json

{
    "user": {
        "userId": 123,
        "username": "newusername",
        "email": "new@email.com"
    }
}
```

**What happens:**
1. JWT validated ? UserContext created
2. `user` parameter deserialized from JSON
3. `UpdateUserAsync(User user, UserContext userContext)` called
4. Service validates user can update this profile
5. Updates if authorized

### 6. User Context Properties Available in All Services

```csharp
public class UserContext
{
    public string? UserId { get; set; }           // "123"
    public string Username { get; set; }          // "john.doe"  
    public string Email { get; set; }             // "john@example.com"
    public string Role { get; set; }              // "Admin" or "User"
    public string FullName { get; set; }          // "John Doe"
    public bool IsAuthenticated { get; set; }     // true/false
    public Dictionary<string, string> Claims      // All JWT claims
    
    // Helper methods
    public bool IsInRole(string role)             // Check user role
    public bool HasClaim(string claimType)        // Check if claim exists  
    public string? GetClaimValue(string claimType) // Get claim value
}
```

### 7. Security Features

#### Automatic Authorization Checks
- Services can check `userContext.IsAuthenticated`
- Role-based access via `userContext.IsInRole("Admin")`
- Custom claim validation

#### Per-User Data Filtering
```csharp
// Example: Users can only see their own data
public async Task<IEnumerable<Order>> GetMyOrdersAsync(UserContext userContext)
{
    if (!userContext.IsAuthenticated) 
        throw new UnauthorizedAccessException();
        
    // Filter by current user ID
    return await GetOrdersByUserId(int.Parse(userContext.UserId));
}
```

#### Audit Logging
```csharp
_logger.LogInformation("User {UserId} accessed {Method}", 
    userContext.UserId, nameof(GetAllUsersAsync));
```

## ? **Answer to Your Question:**

**YES, logged-in user details ARE available for ALL services through:**

1. **UserContext parameter** - Automatically injected by DynamicServiceMiddleware
2. **HttpContext.Items** - Available in any middleware/controller  
3. **ClaimsPrincipal** - Standard ASP.NET Core user object
4. **Extension methods** - Helper methods for easy access

The user information flows through the entire request pipeline and is accessible at every layer of your application!