# OpenAPI-CodeGenned: Contract-First Weather API

A demonstration of **Contract-First Development** in .NET 9, featuring a custom **Roslyn Source Generator** that scaffolds a complete CRUD API directly from an OpenAPI specification.

## 🚀 Key Features

-   **Contract-First Workflow**: The `openapi.yaml` file is the absolute source of truth. Changes to the spec automatically update C# models and endpoint structures on build.
-   **Custom Roslyn Source Generator**: A high-performance `IIncrementalGenerator` that parses YAML and emits C# code at compile-time.
    -   **Zero-Dependency YAML Parsing**: Includes a lightweight, built-in YAML parser to keep the analyzer assembly lean.
    -   **Intelligent Request Merging**: Automatically merges URL path parameters (e.g., `{id}`) and JSON request bodies into unified C# records.
-   **FastEndpoints Integration**: Leverages the [FastEndpoints](https://fast-endpoints.com/) library for a clean REPR (Request-Endpoint-Response) architectural pattern.
-   **Minimal Boilerplate**: Developers only implement the "hand-written" logic in partial classes; routing, DTOs, and configuration are all generated.

## 🏗️ Project Structure

-   **`src/OpenApiSourceGenerator`**: The Roslyn Source Generator project.
-   **`src/WeatherApi`**: The main Web API project.
    -   `openapi.yaml`: The API specification.
    -   `Endpoints/`: Manual implementations of the generated partial classes.
    -   `Data/`: Simple in-memory data store for demonstration.

## 🛠️ Getting Started

### Prerequisites

-   [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Running the API

1.  Clone the repository.
2.  Navigate to the project root.
3.  Run the API:
    ```bash
    dotnet run --project src/WeatherApi
    ```
4.  The API will be available at `http://localhost:5000` (or the port specified in `launchSettings.json`).

### Testing the Endpoints

You can use the provided `WeatherApi.http` file or any API client (Postman, curl) to interact with the endpoints:

-   `GET /weather`: List all records.
-   `POST /weather`: Create a new record.
-   `GET /weather/{id}`: Get a specific record.
-   `PUT /weather/{id}`: Update a record.
-   `DELETE /weather/{id}`: Delete a record.

## 📝 How it Works

### 1. Define the Spec
Add or modify an operation in `src/WeatherApi/openapi.yaml`.

### 2. Automatic Scaffolding
On build, the generator produces:
-   **Models**: `WeatherRecord`, `CreateWeatherRequest`, etc.
-   **Endpoints**: `GetWeatherEndpoint`, `UpdateWeatherEndpoint`, etc., with all routing pre-configured.

### 3. Implement Logic
Extend the generated partial class to add your business logic:

```csharp
// src/WeatherApi/Endpoints/GetWeatherEndpoint.cs
public partial class GetWeatherEndpoint
{
    public override async Task HandleAsync(GetWeatherRequest req, CancellationToken ct)
    {
        var record = WeatherStore.GetById(req.Id);
        if (record == null)
        {
            await SendNotFoundAsync(ct);
            return;
        }
        await SendOkAsync(record, ct);
    }
}
```

## 🛡️ Security Note

This is a demonstration project using in-memory storage. It is intended for educational purposes and as a template for more complex source-generated architectures. For production use, ensure proper authentication, authorization, and persistent database integration.

## 📜 License

MIT
