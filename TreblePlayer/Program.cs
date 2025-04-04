using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.AspNetCore.SignalR;
using TreblePlayer.Data;
using TreblePlayer.Core;
using TreblePlayer.Services;
using TreblePlayer.Models;
using Microsoft.Extensions.Logging;

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
builder.Services.AddScoped<ILoggingService, LoggingService>();
builder.Services.AddSingleton<MusicPlayer>(sp =>
{
    var scopeFactory = sp.GetRequiredService<IServiceScopeFactory>();
    return new MusicPlayer(scopeFactory, sp.GetRequiredService<IHubContext<PlaybackHub>>());
});

var app = builder.Build();

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
