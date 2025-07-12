using articlessvc.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddHttpClient<WikiService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Preload articles on app startup
app.Lifetime.ApplicationStarted.Register(() =>
{
    // Fire-and-forget task; alternatively use Task.Run if needed
    _ = Task.Run(async () =>
    {
        using var scope = app.Services.CreateScope();
        var wikiService = scope.ServiceProvider.GetRequiredService<WikiService>();
        await wikiService.PreloadArticlesAsync(200);
    });
});

app.Run();
