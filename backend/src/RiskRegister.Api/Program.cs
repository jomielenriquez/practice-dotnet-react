var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No UseHttpsRedirection: local dev runs over plain HTTP, so there is no dev
// certificate to trust. The frontend reaches this through the Vite proxy.

app.MapGet("/api/hello", () =>
    TypedResults.Ok(new HelloResponse(
        "Hello from the Risk Register API",
        DateTimeOffset.UtcNow)))
    .WithName("GetHello");

app.Run();

public sealed record HelloResponse(string Message, DateTimeOffset UtcNow);
