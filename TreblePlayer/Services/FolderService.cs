//using Microsoft.EntityFrameworkCore;
//using Microsoft.EntityFramework.DbSet;
//using System.Models.DataDesign;

using System.Linq;
using TreblePlayer.Data;
using TreblePlayer.Models;
using Microsoft.EntityFrameworkCore;
namespace TreblePlayer.Services;


public class FolderService //: IFolderService
{

    private readonly MusicPlayerDbContext _dbContext;
    private static readonly string[] SupportedExtensions = { ".mp3", ".flac", ".alac", ".opus", ".wav", ".aac", ".ogg" };
}
