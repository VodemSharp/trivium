using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/", () => "Trivium.Sample.Webhook placeholder; completed in M9.");

app.Run();
