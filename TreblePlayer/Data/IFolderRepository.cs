using TreblePlayer.Models;
namespace TreblePlayer.Data;

public interface IFolderRepository
{
    List<Album> GetFolders();
    void CreateAlbum(string path, string name, int id);
    Task<bool> AddFolderAsync(string path);
    Task<bool> RemoveFolderAsync(long folderId);
    Task UpdateFoldersAsync(IList<Album> folders);
}
