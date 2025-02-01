//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFramework.DbSet;
//using System.Models.DataDesign;

using System.Linq;
using TreblePlayer.Models;
using Microsoft.EntityFrameworkCore;
namespace TreblePlayer.Data;


public class FolderRepository : IFolderRepository
{

    private readonly MusicPlayerDbContext _dbContext;
    private static readonly string[] SupportedExtensions = { ".mp3", ".flac", ".alac", ".opus", ".wav", ".aac", ".ogg" };

    public List<Album> GetFolders()
    {
        return _dbContext.Albums.ToList();

    }
    public async Task CreateAlbum(string path, string name, int id)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            var newAlbum = new Album
            {
                Id = id,
                Title = name,
                Tracks = new List<Track>()
            };
            await _dbContext.Albums.AddAsync(newAlbum);
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating album: {ex.Message}");
        }
    }
    public async Task<bool> AddFolderAsync(string path)
    {
        if (!Directory.Exists(path))
        {
            Console.WriteLine("Path not found. Invalid Directory.");
            return false;
        }

        var folderName = Path.GetFileName(path);
        var existingAlbum = _dbContext.Albums.FirstOrDefault(a => a.Title == folderName);
        if (existingAlbum == null)
        {
            Console.WriteLine("Album already exists");
            return false;
        }

        var album = new Album
        {
            Title = folderName,
            Tracks = Directory.GetFiles(path, "*.*", SearchOption.AllDirectories)
                .Where(file => SupportedExtensions.Contains(Path.GetExtension(file).ToLower()))
                .Select(file => new Track
                {
                    Title = Path.GetFileNameWithoutExtension(file),
                    FilePath = file,
                    Duration = new ATL.Track(file).Duration
                })
                .ToList()
        };

        await _dbContext.Albums.AddAsync(album);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveFolderAsync(int folderId)
    {
        var album = await _dbContext.Albums.FindAsync(folderId);
        if (album == null)
        {
            Console.WriteLine("No album found.");
            return false;
        }
        _dbContext.Albums.Remove(album);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task UpdateFoldersAsync(IList<Album> folders)
    {
        foreach (var album in folders)
        {
            var existingAlbum = await _dbContext.Albums.FirstOrDefaultAsync(a => a.Id == album.Id);

            if (existingAlbum != null)
            {
                existingAlbum.Title = album.Title;
                existingAlbum.Tracks = album.Tracks;
            }
        }
        await _dbContext.SaveChangesAsync();
    }

}

