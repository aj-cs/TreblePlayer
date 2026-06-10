using System.ComponentModel.DataAnnotations;

namespace TreblePlayer.Models
{
    public class MonitoredFolder
    {
        public int Id { get; set; } // Primary Key

        [Required]
        public string Path { get; set; } = string.Empty;

        public DateTime DateAdded { get; set; }
    }
} 