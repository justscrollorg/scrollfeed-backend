using jokessvc.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();
builder.Services.AddSingleton<JokeService>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var jokeService = scope.ServiceProvider.GetRequiredService<JokeService>();
    Console.WriteLine("Preloading jokes...");
    await jokeService.PreloadJokesAsync(10000);
    Console.WriteLine($"Preloaded {jokeService.TotalJokes()} jokes.");
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthorization();
app.MapControllers();
app.Run();
