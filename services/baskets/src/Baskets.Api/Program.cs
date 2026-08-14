using ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var app = builder.Build();
app.UseServiceDefaults();

// No endpoints yet — health checks land in a later task (T024), routing under
// Features/HealthCheck/. This bare shell exists so the failing tests in this task
// (T008, T012) have something real to run against and fail at runtime, not at compile time.

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
