using TreblePlayer.Models;

namespace TreblePlayer.Services;

public interface IFolderService
{
    List<string> GetFolders(string path);
    List<string> GetAudioFiles(string path);

    bool CreateFolder(string path);
    bool DeleteFolder(string path);
    bool FolderExists(string path);
}
