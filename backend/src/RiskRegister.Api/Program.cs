using System.Text.Json;
using System.Text.Json.Serialization;
using RiskRegister.Api.ErrorHandling;
using RiskRegister.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Without this, Status and Severity serialise as 0..3 and the frontend has numbers to
        // interpret instead of "Open" and "Critical".
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

        // ValidationProblemDetails.Errors is a dictionary keyed by CLR property name, so without
        // this a bad POST body comes back as { "errors": { "Title": [...] } } while the request
        // that caused it used "title". The frontend maps errors onto fields by that key; one
        // casing convention across the whole wire is what makes that lookup safe.
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
    });

// Gives every unhandled failure an RFC 7807 body, matching the ValidationProblemDetails that
// [ApiController] already returns for a bad request. One error shape across the whole API.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DatabaseExceptionHandler>();

var app = builder.Build();

// Must come first: it wraps everything registered after it. Details are never written to the
// response — AddProblemDetails supplies the body, and the handlers log the real exception.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// No UseHttpsRedirection: local dev runs over plain HTTP, so there is no dev
// certificate to trust. The frontend reaches this through the Vite proxy.

app.MapControllers();

app.Run();

// Required for WebApplicationFactory<Program> in RiskRegister.Tests.
public partial class Program;
