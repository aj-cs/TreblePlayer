using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
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

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
builder.Services.AddScoped<IArtistNormalizationService, ArtistNormalizationService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IArtworkService, ArtworkService>();
builder.Services.AddSingleton<LibraryScanProgressService>();
builder.Services.AddSingleton<ILoggingService, LoggingService>();
builder.Services.AddSingleton<MusicPlayer>();
builder.Services.AddSingleton<PlaybackWebSocketHandler>();
builder.Services.AddSingleton<IArtistAliasService, ArtistAliasService>();
builder.Services.AddSingleton<IArtistNormalizationSettingsService, ArtistNormalizationSettingsService>();

// Register OrphanedDataCleanupService as both a Singleton (for controller injection) and a Hosted Service
// This allows it to be injected into controllers while still functioning as a background service
builder.Services.AddSingleton<OrphanedDataCleanupService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<OrphanedDataCleanupService>());

// Register other hosted services
builder.Services.AddSingleton<FolderMonitoringService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FolderMonitoringService>());

var app = builder.Build();

// Allow library reads to continue while the background scanner writes to
// SQLite. WAL is persisted in the database and requires no schema migration.
try
{
    await using (var databaseScope = app.Services.CreateAsyncScope())
    {
        var database = databaseScope.ServiceProvider.GetRequiredService<MusicPlayerDbContext>();
        await database.Database.MigrateAsync();
        await database.Database.OpenConnectionAsync();
        await database.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        Console.WriteLine("SQLite write-ahead logging enabled.");
    }
}
catch (Exception ex)
{
    // Keep startup resilient if the configured database is read-only or sits
    // on a filesystem that does not support SQLite WAL mode.
    Console.WriteLine($"Unable to enable SQLite WAL mode: {ex.Message}");
}

// Ensure artwork directory and placeholders exist
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

// placeholder check helper function
async Task EnsurePlaceholderExists(string placeholderFileName, string resourceName)
{
    var placeholderPath = Path.Combine(artworkBasePath, placeholderFileName);
    if (!File.Exists(placeholderPath))
    {
        Console.WriteLine($"Placeholder '{placeholderFileName}' not found at {placeholderPath}. Attempting to extract from embedded resources...");
        var assembly = Assembly.GetExecutingAssembly();

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
                        await resourceStream.CopyToAsync(fileStream);
                    }
                    Console.WriteLine($"Successfully extracted embedded placeholder '{placeholderFileName}' to: {placeholderPath}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting embedded resource '{resourceName}': {ex.Message}");
        }
    }
}

await EnsurePlaceholderExists("placeholder.png", "TreblePlayer.artwork.placeholder.png");
await EnsurePlaceholderExists("placeholder2.png", "TreblePlayer.artwork.placeholder2.png");

// FolderMonitoringService will automatically scan all monitored folders on startup
// This is handled in the StartAsync method of the FolderMonitoringService class
// No additional startup code is needed here cuz the service is designed to do this automatically

// Configure the HTTP request pipeline.
app.UseMiddleware<TreblePlayer.Middleware.ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowReactApp");

//might not need this ->
//app.UseHttpsRedirection();

app.UseWebSockets();

app.UseAuthorization();

app.MapControllers();

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        var handler = context.RequestServices.GetRequiredService<PlaybackWebSocketHandler>();
        await handler.HandleAsync(context, webSocket);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
    }
});

app.Run();
