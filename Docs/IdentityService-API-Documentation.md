# SmartFit Identity Service API Documentation

## Overview
This document describes the Identity Service API endpoints for the SmartFit application. The service provides comprehensive authentication and authorization functionality using JWT tokens.

## Base URL
```
http://localhost:5000/api/auth
```

## Authentication
Most endpoints require a valid JWT token in the Authorization header:
```
Authorization: Bearer <your-jwt-token>
```

## Endpoints

### 1. User Registration
**POST** `/api/auth/register`

Register a new user account.

**Request Body:**
```json
{
    "username": "johndoe",
    "email": "john@example.com",
    "password": "Password123!",
    "confirmPassword": "Password123!",
    "fullName": "John Doe",
    "phoneNumber": "+1234567890"
}
```

**Response:**
```json
{
    "message": "Registration successful",
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expires": "2024-01-01T12:00:00Z",
    "user": {
        "userId": 1,
        "username": "johndoe",
        "email": "john@example.com",
        "fullName": "John Doe",
        "role": "User",
        "isActive": true
    }
}
```

### 2. User Login
**POST** `/api/auth/login`

Authenticate a user and receive access tokens.

**Request Body:**
```json
{
    "email": "john@example.com",
    "password": "Password123!"
}
```

**Response:**
```json
{
    "message": "Login successful",
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expires": "2024-01-01T12:00:00Z",
    "user": {
        "userId": 1,
        "username": "johndoe",
        "email": "john@example.com",
        "fullName": "John Doe",
        "role": "User",
        "isActive": true
    }
}
```

### 3. Refresh Token
**POST** `/api/auth/refresh-token`

Refresh an expired access token using a refresh token.

**Request Body (Optional - can use cookie):**
```json
{
    "refreshToken": "your-refresh-token"
}
```

**Response:**
```json
{
    "message": "Token refreshed successfully",
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expires": "2024-01-01T12:00:00Z",
    "user": {
        "userId": 1,
        "username": "johndoe",
        "email": "john@example.com",
        "fullName": "John Doe",
        "role": "User",
        "isActive": true
    }
}
```

### 4. Logout
**POST** `/api/auth/logout`

Logout the current user and revoke refresh token.

**Headers:**
```
Authorization: Bearer <your-jwt-token>
```

**Response:**
```json
{
    "message": "Logout successful"
}
```

### 5. Change Password
**POST** `/api/auth/change-password`

Change the current user's password.

**Headers:**
```
Authorization: Bearer <your-jwt-token>
```

**Request Body:**
```json
{
    "currentPassword": "OldPassword123!",
    "newPassword": "NewPassword123!",
    "confirmNewPassword": "NewPassword123!"
}
```

**Response:**
```json
{
    "message": "Password changed successfully"
}
```

### 6. Get Current User
**GET** `/api/auth/me`

Get current authenticated user information.

**Headers:**
```
Authorization: Bearer <your-jwt-token>
```

**Response:**
```json
{
    "userId": 1,
    "username": "johndoe",
    "email": "john@example.com",
    "fullName": "John Doe",
    "role": "User",
    "isActive": true
}
```

### 7. Validate Token
**POST** `/api/auth/validate-token`

Validate if a JWT token is still valid.

**Request Body:**
```json
"eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
```

**Response:**
```json
{
    "isValid": true
}
```

### 8. Revoke Token
**POST** `/api/auth/revoke-token`

Revoke a refresh token.

**Headers:**
```
Authorization: Bearer <your-jwt-token>
```

**Request Body (Optional):**
```json
{
    "refreshToken": "your-refresh-token"
}
```

**Response:**
```json
{
    "message": "Token revoked successfully"
}
```

## Error Responses

All endpoints may return the following error responses:

### 400 Bad Request
```json
{
    "message": "Validation failed",
    "errors": {
        "Email": ["The Email field is required."],
        "Password": ["The Password field must be at least 6 characters."]
    }
}
```

### 401 Unauthorized
```json
{
    "message": "Invalid email or password"
}
```

### 500 Internal Server Error
```json
{
    "message": "An error occurred during registration"
}
```

## JWT Token Claims

The JWT tokens contain the following claims:

- `nameid`: User ID
- `unique_name`: Username
- `email`: User email
- `role`: User role (User, Admin)
- `FullName`: User's full name
- `jti`: JWT ID
- `iat`: Issued at timestamp
- `exp`: Expiration timestamp
- `iss`: Issuer
- `aud`: Audience

## Security Notes

1. **HTTPS**: Always use HTTPS in production
2. **Token Storage**: Store JWT tokens securely (not in localStorage for web apps)
3. **Refresh Tokens**: Refresh tokens are stored in HTTP-only cookies
4. **Token Expiration**: Access tokens expire in 60 minutes, refresh tokens in 7 days
5. **Password Hashing**: Passwords are hashed using BCrypt
6. **Rate Limiting**: Consider implementing rate limiting for authentication endpoints

## Integration with Dynamic Service Middleware

The Identity Service integrates with your existing Dynamic Service Middleware. Once authenticated, users can access other services through the middleware using their JWT tokens.

Example service call:
```
POST /api/services/UserService/GetAllUsersAsync
Authorization: Bearer <your-jwt-token>
Content-Type: application/json

{
    "someParameter": "value"
}
```