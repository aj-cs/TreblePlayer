﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TreblePlayer.Data;

#nullable disable

namespace TreblePlayer.Migrations
{
    [DbContext(typeof(MusicPlayerDbContext))]
    partial class MusicPlayerDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.10");

            modelBuilder.Entity("TreblePlayer.Models.Album", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("AlbumArtist")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("TEXT");

                    b.Property<string>("FolderPath")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Genre")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<DateTime>("LastModified")
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Albums");
                });

            modelBuilder.Entity("TreblePlayer.Models.Track", b =>
                {
                    b.Property<int>("TrackId")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("AlbumId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Artist")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Bitrate")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("DateAdded")
                        .HasColumnType("TEXT");

                    b.Property<string>("FilePath")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Genre")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int?>("TrackQueueId")
                        .HasColumnType("INTEGER");

                    b.HasKey("TrackId");

                    b.HasIndex("AlbumId");

                    b.HasIndex("TrackQueueId");

                    b.ToTable("Tracks");
                });

            modelBuilder.Entity("TreblePlayer.Models.TrackQueue", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("DateCreated")
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsSessionQueue")
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("LastModified")
                        .HasColumnType("TEXT");

                    b.Property<string>("Title")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("TrackQueues");
                });

            modelBuilder.Entity("TreblePlayer.Models.Track", b =>
                {
                    b.HasOne("TreblePlayer.Models.Album", "Album")
                        .WithMany("Tracks")
                        .HasForeignKey("AlbumId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("TreblePlayer.Models.TrackQueue", null)
                        .WithMany("Tracks")
                        .HasForeignKey("TrackQueueId");

                    b.Navigation("Album");
                });

            modelBuilder.Entity("TreblePlayer.Models.Album", b =>
                {
                    b.Navigation("Tracks");
                });

            modelBuilder.Entity("TreblePlayer.Models.TrackQueue", b =>
                {
                    b.Navigation("Tracks");
                });
#pragma warning restore 612, 618
        }
    }
}
