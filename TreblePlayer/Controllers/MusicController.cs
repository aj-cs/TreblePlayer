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
            bool resumed = _player.Resume();
            if (resumed)
            {
                return Ok(new { message = "Playback resumed" });
            }
            else
            {
                return BadRequest(new { message = "No paused track to resume" });
            }
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
            bool paused = _player.Pause();
            if (paused)
            {
                return Ok(new { message = "Paused music." });
            }
            else
            {
                return BadRequest(new { message = "No playing track to pause." });
            }
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
            bool stopped = _player.Stop();
            if (stopped)
            {
                return Ok(new { message = "Stopped music." });
            }
            else
            {
                return BadRequest(new { message = "No playing track to stop." });
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e); // Log full exception to console
            return StatusCode(500, new { message = "An unexpected error occurred." });
        }
    }
    [HttpPost("next")]
    public async Task<IActionResult> PlayNext()
    {
        try
        {
            await _player.NextAsync();
            return Ok(new { message = "Next track playing." });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return StatusCode(500, new { message = "An error occured." });
        }

    }

    [HttpPost("previous")]
    public async Task<IActionResult> PlayPrevious()
    {
        try
        {
            await _player.PreviousAsync();
            return Ok(new { message = "Previous track playing." });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return StatusCode(500, new { message = "An error occured." });
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
    [HttpPost("shuffle/{enable}")]
    public IActionResult EnableShuffle(bool enable)
    {
        _player.EnableShuffle(enable);
        return Ok(new { message = $"Shuffle {(enable ? "enabled" : "disabled")}" });
    }

    [HttpPost("loop/{enable}")]
    public IActionResult EnableLoop(bool enable)
    {
        _player.EnableLoop(enable);
        return Ok(new { message = $"Loop {(enable ? "enabled" : "disabled")}" });
    }

    [HttpPost("loop/set/{mode}")]
    public async Task<IActionResult> SetLoopMode(int mode)
    {
        try
        {
            await _player.SetLoopModeAsync((LoopTrack)mode);
            return Ok(new { message = $"Loop mode set to {mode}" });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return StatusCode(500, new { message = "Failed to set loop mode." });
        }
    }

    [HttpPost("loop/toggle")]
    public async Task<IActionResult> ToggleLoopMode()
    {
        try
        {
            var newMode = await _player.ToggleLoopModeAsync();
            return Ok(new { message = $"Loop mode toggled to {newMode}" });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return StatusCode(500, new { message = "Failed to toggle loop mode." });
        }
    }


}
