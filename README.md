# Task Management API (.NET Core)

This is the backend API for the Task Management Sprint System, built with .NET Core.

## Overview

This API provides endpoints for managing:
- User authentication and authorization
- Task management
- Sprint management
- Custom middleware for request/response handling

## Technologies Used

- .NET Core (ASP.NET Core Web API)
- JWT Authentication
- Custom Middleware Components

## Prerequisites

- .NET 8.0 SDK or later
- Visual Studio 2022 or VS Code (optional)

## Getting Started

### Installation

1. Navigate to the API directory:
```bash
cd task-management-api
```

2. Restore dependencies:
```bash
dotnet restore
```

3. Build the project:
```bash
dotnet build
```

### Running the Application

To run the API in development mode:
```bash
dotnet run
```

The API will be available at `http://localhost:5000` or `https://localhost:5001`

### Testing

Run the tests (if available):
```bash
dotnet test
```

## Project Structure

- `Controllers/` - API endpoint controllers
- `MiddleWare/` - Custom middleware components
- `Models/` - Data models
- `Services/` - Business logic services
- `data/` - Data storage/configuration
- `appsettings.json` - Application configuration

## API Endpoints

Refer to the `MiddleWareWebApi.http` file for example API requests.

## Configuration

Update `appsettings.json` and `appsettings.Development.json` for environment-specific settings.

## Middleware Components

This project includes custom middleware for:
- JWT Authentication
- Role-based Authorization
- Dynamic Service handling
- Response transformation
- Request/Response logging

## Contributing

Please follow .NET coding conventions and ensure all builds pass before committing.
