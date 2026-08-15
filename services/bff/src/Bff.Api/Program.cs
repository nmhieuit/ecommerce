var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.Run();

public partial class Program; // exposes the entry point to WebApplicationFactory<Program> in tests
