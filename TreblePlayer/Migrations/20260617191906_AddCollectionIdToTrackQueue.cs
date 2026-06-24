using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreblePlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddCollectionIdToTrackQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CollectionId",
                table: "TrackQueues",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "TrackQueues");
        }
    }
}
