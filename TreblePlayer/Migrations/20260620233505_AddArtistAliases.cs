using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TreblePlayer.Migrations
{
    /// <inheritdoc />
    public partial class AddArtistAliases : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArtistAliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AliasName = table.Column<string>(type: "TEXT", nullable: false),
                    CanonicalName = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArtistAliases", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArtistAliases_AliasName",
                table: "ArtistAliases",
                column: "AliasName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArtistAliases");
        }
    }
}
