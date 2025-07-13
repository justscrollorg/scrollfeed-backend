using articlessvc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddSingleton<WikiService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Explicit preload BEFORE app.Run()
using (var scope = app.Services.CreateScope())
{
    var wikiService = scope.ServiceProvider.GetRequiredService<WikiService>();
    Console.WriteLine("Preloading articles...");
    await wikiService.PreloadArticlesAsync(200);
    Console.WriteLine($"Preloaded {wikiService.TotalArticles()} articles.");
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthorization();
app.MapControllers();
app.Run();
