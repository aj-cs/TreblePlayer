using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Cryptography.Xml;
using TreblePlayer.Core;
using TreblePlayer.Data;
using TreblePlayer.Models;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
namespace TreblePlayer.Controllers;


[ApiController]
[Route("api/[controller]")]
public class MusicController : ControllerBase
{
    //private readonly AppDbContext _appDbContext;
    private readonly MusicPlayer _player;
    public MusicController(MusicPlayer musicPlayer)
    {
        _player = musicPlayer;
    }

    [HttpPost("play/{trackId}")]
    public async Task<IActionResult> PlayAsync(int trackId)
    {
        try
        {
            await _player.PlayAsync(trackId);
            return Ok(new { message = $"Playing track (ID: {trackId})" });
        }
        catch (Exception e)
        {
            Console.WriteLine(e); // Log full exception to console
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }
    [HttpPost("resume")]
    public IActionResult Resume()
    {
        try
        {
            _player.Resume();
            return Ok(new { message = "Playback resumed" });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(500, new { message = ex.Message });
        }
    }


    [HttpPost("pause")]
    public IActionResult Pause()
    {
        try
        {
            _player.Pause();
            return Ok(new { message = "Paused music." });
        }
        catch (Exception e)
        {
            Console.WriteLine(e); // Log full exception to console
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }

    [HttpPost("stop")]
    public IActionResult Stop()
    {
        try
        {
            _player.Stop();
            return Ok(new { message = "Stopped music." });
        }
        catch (Exception e)
        {
            Console.WriteLine(e); // Log full exception to console
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }
    [HttpPost("seek/{seconds}")]
    public IActionResult Seek(float seconds)
    {
        try
        {
            _player.Seek(seconds);
            return Ok(new { message = $"Seeked to {seconds} seconds." });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(500, new { message = "Failed to seek." });
        }
    }
    [HttpPost("playCollection/{collectionId}/{type}")]
    public async Task<IActionResult> PlayCollection(int collectionId, TrackCollectionType type)
    {
        try
        {
            await _player.PlayCollectionAsync(collectionId, type);
            return Ok(new { message = $"Playing collection {collectionId} of type {type}" });
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(500, new { message = $"Failed to play collection {collectionId} of type {type}" });
        }
    }

    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        var isPlaying = _player.IsPlaying();
        return Ok(new { message = $"Music playing: {isPlaying}" });
    }

    [HttpPost("queue/create")]
    public async Task<IActionResult> CreateQueue(string title) //change later, 
    // queues should be created from track(s) aka IEnumerable/ICollection<Track> or an ITrackCollection
    {
        try
        {
            await _player.CreateQueueAsync(title);
            return Ok(new { message = $"Queue {title} created." });
        }

        catch (Exception e)
        {
            Console.WriteLine(e); // Log full exception to console
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }

    }
    [HttpPost("queue/{queueId}/addTrack/{trackId}")]
    public async Task<IActionResult> AddTrackToQueue(int queueId, int trackId)
    {
        try
        {
            await _player.AddTrackToQueueAsync(queueId, trackId);
            return Ok(new { message = $"Track (ID: {trackId} added to Queue (ID: {queueId})" });
        }
        catch (Exception e)
        {
            Console.WriteLine(e); // Log full exception to console
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }


}
