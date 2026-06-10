using Microsoft.AspNetCore.SignalR;
namespace TreblePlayer.Core;

public class DataHub : Hub
{
    private readonly MusicPlayer _player;
    public DataHub(MusicPlayer player)
    {
        _player = player;
    }
}



