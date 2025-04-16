using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.AspNetCore.SignalR;
using TreblePlayer.Data;
using TreblePlayer.Core;
using TreblePlayer.Services;
using TreblePlayer.Models;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
//needed for react ->
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", builder =>
        builder.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});
builder.Services.AddDbContext<MusicPlayerDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<ITrackRepository, TrackRepository>();
builder.Services.AddScoped<ITrackCollectionRepository, TrackCollectionRepository>();
// builder.Services.AddScoped<IFolderService, FolderService>();

builder.Services.AddScoped<IMetadataService, MetadataService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IArtworkService, ArtworkService>();
builder.Services.AddSingleton<ILoggingService, LoggingService>();
builder.Services.AddSingleton<MusicPlayer>(sp =>
{
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    var hubContext = sp.GetRequiredService<IHubContext<PlaybackHub>>();
    var logger = sp.GetRequiredService<ILoggingService>();
    return new MusicPlayer(scopeFactory, hubContext, logger);
});

var app = builder.Build();

// Ensure artwork directory and placeholder exists
var artworkBasePath = Path.Combine(AppContext.BaseDirectory, "artwork");
if (!Directory.Exists(artworkBasePath))
{
    try
    {
        Directory.CreateDirectory(artworkBasePath);
        Console.WriteLine($"Created artwork directory at: {artworkBasePath}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error creating artwork directory: {ex.Message}");
        // Decide if you want to throw or continue if directory creation fails
    }
}

var placeholderPath = Path.Combine(artworkBasePath, "placeholder.png");
if (!File.Exists(placeholderPath))
{
    Console.WriteLine($"Placeholder file not found at {placeholderPath}. Attempting to extract from embedded resources...");
    var assembly = Assembly.GetExecutingAssembly();
    // IMPORTANT: Ensure this matches the LogicalName in your .csproj
    var resourceName = "TreblePlayer.artwork.placeholder.png"; 

    try
    {
        using (Stream? resourceStream = assembly.GetManifestResourceStream(resourceName))
        {
            if (resourceStream == null)
            {
                Console.WriteLine($"Error: Embedded resource '{resourceName}' not found in assembly.");
            }
            else
            {
                using (var fileStream = new FileStream(placeholderPath, FileMode.Create, FileAccess.Write))
                {
                    await resourceStream.CopyToAsync(fileStream); // Use async version
                }
                Console.WriteLine($"Successfully extracted embedded placeholder to: {placeholderPath}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error extracting embedded resource '{resourceName}': {ex.Message}");
        // Log the error, the application might still function but without a default image
    }
}

using (var scope = app.Services.CreateScope())
{
    var metadataService = scope.ServiceProvider.GetRequiredService<IMetadataService>();
    string folderPath = Path.Combine(Directory.GetCurrentDirectory(), "music");
    await metadataService.ScanMusicFolderAsync(folderPath);
}
// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//Needed for react ->
app.UseCors("AllowReactApp");

//might not need this ->
app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();
app.MapHub<PlaybackHub>("/treblehub");
app.Run();
