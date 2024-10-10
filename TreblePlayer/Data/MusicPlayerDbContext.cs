namespace TreblePlayer.Data;
using TreblePlayer.Models;
using Microsoft.EntityFrameworkCore;
public class MusicPlayerDbContext : DbContext
{
    public MusicPlayerDbContext(DbContextOptions<MusicPlayerDbContext> options) : base(options) { }
    public DbSet<Album> Albums { get; set; }
    public DbSet<Track> Tracks { get; set; }
    //public DbSet<Playlist> Playlists { get; set; }
    public DbSet<TrackQueue> TrackQueues { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Album-Track relationship
        modelBuilder.Entity<Album>()
           .HasKey(a => a.Id);

        modelBuilder.Entity<Track>()
           .HasKey(t => t.TrackId);

        modelBuilder.Entity<Track>()
           .HasOne(t => t.Album)
           .WithMany(a => a.Tracks)
           .HasForeignKey(t => t.AlbumId);
        /*
        Playlist-Track relationship
        modelBuilder.Entity<Playlist>()
           .HasKey(p => p.Id);

        modelBuilder.Entity<Playlist>()
           .HasMany(p => p.Tracks)
           .WithMany(); // Many-to-many: A playlist can have many tracks, and tracks can belong to multiple playlists

        Queue-Track relationship
        /*modelBuilder.Entity<TrackQueue>()
           .HasKey(q => q.Id);

        modelBuilder.Entity<TrackQueue>()
           .HasOne(q => q.Track)
           .WithMany(); // Each queue entry refers to one track
           */
    }
}
