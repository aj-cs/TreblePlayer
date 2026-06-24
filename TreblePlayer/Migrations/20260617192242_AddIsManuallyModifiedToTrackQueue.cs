using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreblePlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddIsManuallyModifiedToTrackQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManuallyModified",
                table: "TrackQueues",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManuallyModified",
                table: "TrackQueues");
        }
    }
}
