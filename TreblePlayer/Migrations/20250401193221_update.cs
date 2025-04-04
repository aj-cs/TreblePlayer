using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreblePlayer.Migrations
{
    /// <inheritdoc />
    public partial class update : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentTrackIndex",
                table: "TrackQueues",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLoopEnabled",
                table: "TrackQueues",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsShuffleEnabled",
                table: "TrackQueues",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<float>(
                name: "LastPlaybackPositionSeconds",
                table: "TrackQueues",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LoopTrack",
                table: "TrackQueues",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ShuffledTrackIds",
                table: "TrackQueues",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentTrackIndex",
                table: "TrackQueues");

            migrationBuilder.DropColumn(
                name: "IsLoopEnabled",
                table: "TrackQueues");

            migrationBuilder.DropColumn(
                name: "IsShuffleEnabled",
                table: "TrackQueues");

            migrationBuilder.DropColumn(
                name: "LastPlaybackPositionSeconds",
                table: "TrackQueues");

            migrationBuilder.DropColumn(
                name: "LoopTrack",
                table: "TrackQueues");

            migrationBuilder.DropColumn(
                name: "ShuffledTrackIds",
                table: "TrackQueues");
        }
    }
}
