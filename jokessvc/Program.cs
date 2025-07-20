using jokessvc.BackgroundServices;
using jokessvc.Models;
using jokessvc.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure JokeConfig - similar to WikiConfig in articles service
builder.Services.Configure<JokeConfig>(builder.Configuration.GetSection("Joke"));

// Register services
builder.Services.AddHttpClient();
builder.Services.AddSingleton<JokeService>();

// Register background service
builder.Services.AddHostedService<BackgroundRefreshService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for frontend access
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Middleware
app.UseCors("AllowAll");

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Health endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
