using TreblePlayer.Models;
namespace TreblePlayer.Data;

public interface IFolderRepository
{
    List<Album> GetFolders();
    Task CreateAlbum(string path, string name, int id);
    Task<bool> AddFolderAsync(string path);
    Task<bool> RemoveFolderAsync(int folderId);
    Task UpdateFoldersAsync(IList<Album> folders);
}
