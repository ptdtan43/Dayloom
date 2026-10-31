using Dayloom.Api.Features.Auth;
using Dayloom.Api.Features.Health;
using Dayloom.Application;
using Dayloom.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Composition root: the API is the only place that knows every layer and wires them together.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

// Every error response (validation, 404, unhandled exception) uses the same RFC 9457 problem format.
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Bad input (malformed JSON, wrong content type) is the client's fault: keep its 4xx status instead of 500.
    // Minimal APIs throw these as exceptions in Development, so without this a broken body would look like a crash.
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode
        : StatusCodes.Status500InternalServerError,
});
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthEndpoints();
app.MapAuthEndpoints();

app.Run();
