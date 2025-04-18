using Microsoft.AspNetCore.SignalR;
using TreblePlayer.Models;
namespace TreblePlayer.Core;

public class PlaybackHub : Hub
{
    private readonly MusicPlayer _player;
    public PlaybackHub(MusicPlayer player)
    {
        _player = player;
    }
    //
    // client calls this when user hits Next on the frontend
    public async Task RequestNextTrack() => await _player.NextAsync();

    // client calls this when user hits Previous on the frontend
    public async Task RequestPreviousTrack() => await _player.PreviousAsync();

    public Task RequestPause()
    {
        _player.Pause();
        return Task.CompletedTask;
    }

    public Task RequestResume()
    {
        _player.Resume();
        return Task.CompletedTask;
    }

    public Task RequestStop()
    {
        _player.Stop();
        return Task.CompletedTask;
    }
    public Task RequestSeek(float seconds)
    {
        _player.Seek(seconds);
        return Task.CompletedTask;
    }

    public async Task RequestPlay(int trackId)
    {
        await _player.PlayAsync(trackId);
    }

    public async Task RequestPlayCollection(int collectionId, int type, int startIndex = 0)
    {
        await _player.PlayCollectionAsync(collectionId, (TrackCollectionType)type, startIndex);
    }

    public Task RequestEnableShuffle(bool enable)
    {
        _player.EnableShuffle(enable);
        return Task.CompletedTask;
    }

    public Task RequestEnableLoop(bool enable)
    {
        _player.EnableLoop(enable);
        return Task.CompletedTask;
    }

    public async Task RequestSetLoopMode(int mode)
    {
        await _player.SetLoopModeAsync((LoopTrack)mode);
    }

    public async Task<int> RequestToggleLoopMode()
    {
        return (int)await _player.ToggleLoopModeAsync();
    }

    public async Task RequestPlayPlaylist(int playlistId, int startIndex = 0)
    {
        await _player.PlayPlaylistAsync(playlistId, startIndex);
    }
}

