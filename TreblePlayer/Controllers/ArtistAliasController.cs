using TreblePlayer.Services;
using TreblePlayer.DTOs;
using TreblePlayer.Data;
using TreblePlayer.Models;
using TreblePlayer.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TreblePlayer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtistAliasController : ControllerBase
{
    private readonly ILoggingService _loggingService;
    private readonly IArtistAliasService _aliasService;
    private readonly IArtistNormalizationSettingsService _normalizationSettings;
    private readonly PlaybackWebSocketHandler _webSocketHandler;
    public ArtistAliasController(
        IArtistAliasService aliasService,
        ILoggingService loggingService,
        IArtistNormalizationSettingsService normalizationSettings,
        PlaybackWebSocketHandler webSocketHandler)
    {
        _aliasService = aliasService;
        _loggingService = loggingService;
        _normalizationSettings = normalizationSettings;
        _webSocketHandler = webSocketHandler;
    }

    [HttpGet("aliases")]
    public IActionResult GetAllAliases()
    {
        var aliases = _aliasService.GetAllAliases();

        return Ok(aliases);
    }

    [HttpPost("aliases/add")]
    public async Task<IActionResult> AddArtistAlias([FromBody] ArtistAliasDto dto)
    {
        try
        {
            await _aliasService.AddAliasAsync(dto.Alias, dto.CanonicalName);
            _webSocketHandler.BroadcastNotification("ArtistAliasesUpdated");
            return Ok(new { message = $"{dto.Alias} alias mapped to {dto.CanonicalName} successfully" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("aliases/delete")]
    public async Task<IActionResult> DeleteArtistAlias([FromBody] ArtistAliasDto dto)
    {
        await _aliasService.RemoveAliasAsync(dto.Alias, dto.CanonicalName);
        _webSocketHandler.BroadcastNotification("ArtistAliasesUpdated");
        return Ok(new { message = $"Deleted {dto.Alias} from {dto.CanonicalName}" });
    }

    [HttpGet("featuring-keywords")]
    public IActionResult GetFeaturingKeywords()
    {
        return Ok(_normalizationSettings.GetFeaturingKeywords());
    }

    [HttpPut("featuring-keywords")]
    public IActionResult SetFeaturingKeywords([FromBody] FeaturingKeywordsDto dto)
    {
        if (dto?.Keywords == null) return BadRequest(new { message = "Keywords are required." });
        _normalizationSettings.SetFeaturingKeywords(dto.Keywords);
        _webSocketHandler.BroadcastNotification("ArtistNormalizationSettingsUpdated");
        return Ok(_normalizationSettings.GetFeaturingKeywords());
    }
}
