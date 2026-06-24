using TreblePlayer.Services;
using TreblePlayer.DTOs;
using TreblePlayer.Data;
using TreblePlayer.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace TreblePlayer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ArtistAliasController : ControllerBase
{
    private readonly ILoggingService _loggingService;
    private readonly IArtistAliasService _aliasService;
    public ArtistAliasController(IArtistAliasService aliasService, ILoggingService loggingService)
    {
        _aliasService = aliasService;
        _loggingService = loggingService;
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
        await _aliasService.AddAliasAsync(dto.Alias, dto.CanonicalName);
        return Ok(new { message = $"{dto.Alias} Alias added to {dto.CanonicalName} successfully" });
    }

    [HttpDelete("aliases/delete")]
    public async Task<IActionResult> DeleteArtistAlias([FromBody] ArtistAliasDto dto)
    {
        await _aliasService.RemoveAliasAsync(dto.Alias, dto.CanonicalName);
        return Ok(new { message = $"Deleted {dto.Alias} from {dto.CanonicalName}" });
    }
}
