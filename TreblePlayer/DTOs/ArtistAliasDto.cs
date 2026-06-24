namespace TreblePlayer.DTOs;

public record ArtistAliasDto
{
    public string Alias { get; set; } = string.Empty;
    public string CanonicalName { get; set; } = string.Empty;
}
