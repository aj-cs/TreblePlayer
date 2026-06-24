using System.ComponentModel.DataAnnotations;

namespace TreblePlayer.Models
{
    /// <summary>
    /// Represents a mapping from an alias name to its canonical artist name.
    /// </summary>
    public class ArtistAlias
    {
        /// <summary>
        /// Primary key.
        /// </summary>
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// The alias name (e.g., "Viktor Vaughn", "Madvillain", "King Geedorah").
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string AliasName { get; set; } = string.Empty;

        /// <summary>
        /// The canonical name that all aliases resolve to (e.g., "MF DOOM").
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string CanonicalName { get; set; } = string.Empty;
    }
}
