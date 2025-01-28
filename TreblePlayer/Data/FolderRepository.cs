//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFramework.DbSet;
//using System.Models.DataDesign;

using System.Linq;
using TreblePlayer.Models;
using ATL.AudioData;
using ATL;
namespace TreblePlayer.Data;

public class FolderRepository : IFolderRepository
{

    private readonly MusicPlayerDbContext _dbContext;

    public List<Album> GetFolders()
    {
        return _dbContext.Albums.ToList();

    }
    public void CreateAlbum(string path, string name, int id)
    {
        try
        {
            Directory.CreateDirectory(path);
        }
    }
    public async Task<bool> AddFolderAsync(string path)
    {
        // prolly should add new parameters to IFolderRepo rather than just path
        //try
        //{
        //    Directory.CreateDirectory(path);
        //}
        //catch (Exception e)
        //{
        //}
    }

}

