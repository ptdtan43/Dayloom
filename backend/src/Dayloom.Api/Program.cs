using Dayloom.Api.Features.Health;
using Dayloom.Application;
using Dayloom.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Composition root: the API is the only place that knows every layer and wires them together.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthEndpoints();

app.Run();
