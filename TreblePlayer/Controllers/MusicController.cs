using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Security.Cryptography.Xml;
using TreblePlayer.Core;
using TreblePlayer.Data;
using TreblePlayer.Models;
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
    public async Task<IActionResult> PlayMusic(int trackId)
    {
        try {

            await _player.PlayAsync(trackId);
            return Ok(new { message = $"Playing track (ID: {trackId})" });
        }
        catch (Exception e) {
            return StatusCode(500, new { message = e.Message });
        }
    }


    [HttpPost("pause")]
    public async Task<IActionResult> Pause()
    
    {
        try {

            await _player.PauseAsync();
            return Ok(new { message = "Paused music." });
        }
        catch (Exception e) {
            return StatusCode(500, new { message = "Error pausing music." });
        }
    }
    
    [HttpPost("stop")]
    public async Task<IActionResult> Stop()
    {
        try {

            await _player.StopAsync();
            return Ok(new { message = "Stopped music." });
        }
        catch (Exception e) {
            return StatusCode(500, new { message = "Error stopping music." });
        }
    }

    [HttpGet("status")]
    public IActionResult GetMusicPlayerStatus()
    {
        var isPlaying = _player.IsPlaying();
        return Ok(new { message = $"Music playing: {isPlaying}" });
    }

    [HttpPost("queue/create")]
    public async Task<IActionResult> CreateQueue(string title) //change later, 
    // queues should be created from track(s) aka IEnumerable/ICollection<Track> or an ITrackCollection
    {
        
        try {
            await _player.CreateQueueAsync(title);
            return Ok(new { message = $"Queue {title} created." });
        }
        catch (Exception e)
        {
            return StatusCode(500, new { message = e.Message });
        }

    }
    [HttpPost("queue/{queueId}/addTrack/{trackId}")]
    public async Task<IActionResult> AddTrackToQueue(int queueId, int trackId)
    {
        try {
            await _player.AddTrackToQueueAsync(queueId, trackId);
            return Ok(new { message = $"Track (ID: {trackId} added to Queue (ID: {queueId})" });
        }
        catch (Exception e) {
            return StatusCode(500, new { message = e.Message });
        }
    }


}
