using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreblePlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddProvenanceAndResumptionFieldsToTrackQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastPlayedTrackId",
                table: "TrackQueues",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "OriginCollectionType",
                table: "TrackQueues",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastPlayedTrackId",
                table: "TrackQueues");

            migrationBuilder.DropColumn(
                name: "OriginCollectionType",
                table: "TrackQueues");
        }
    }
}
