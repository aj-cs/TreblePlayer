namespace TreblePlayer.Data;
using TreblePlayer.Models;
using Microsoft.EntityFrameworkCore;
public class MusicPlayerDbContext : DbContext
{
    public MusicPlayerDbContext(DbContextOptions<MusicPlayerDbContext> options) : base(options) { }

    public DbSet<Track> Tracks { get; set; }
    public DbSet<Album> Albums { get; set; }
    public DbSet<Playlist> Playlists { get; set; }
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

        //Playlist-Track relationship
        modelBuilder.Entity<Playlist>()
           .HasKey(p => p.Id);

        modelBuilder.Entity<Playlist>()
           .HasMany(p => p.Tracks)
           .WithMany(t => t.Playlists)  // track can be in multiple playlists
           .UsingEntity<Dictionary<string, object>>(
                 "PlaylistTrack",
                 j => j.HasOne<Track>()
                 .WithMany()
                 .HasForeignKey("TrackId"),

                 j => j.HasOne<Playlist>()
                 .WithMany()
                 .HasForeignKey("PlaylistId")
                 );
        // many to many,  playlist can have many tracks, and tracks can belong to multiple playlists

        //Queue-Track relationship
        modelBuilder.Entity<TrackQueue>()
           .HasKey(q => q.Id);

        // one to many 
        modelBuilder.Entity<TrackQueue>()
           .HasMany(q => q.Tracks)
           .WithMany(t => t.TrackQueues)
           .UsingEntity<Dictionary<string, object>>(
                 "TrackQueueTrack",
                 j => j.HasOne<Track>()
                 .WithMany()
                 .HasForeignKey("TrackId"),

                 j => j.HasOne<TrackQueue>()
                 .WithMany()
                 .HasForeignKey("TrackQueueId")
                 );
    }
}
