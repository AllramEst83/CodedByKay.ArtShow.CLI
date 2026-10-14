using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodedByKay.ArtShow.CLI.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddImageMetadataAndCollectionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Blurb",
                table: "Classifiers",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CoverArtworkId",
                table: "Classifiers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "Classifiers",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DisplayR2Key",
                table: "Artworks",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "Artworks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "Artworks",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Blurb",
                table: "Classifiers");

            migrationBuilder.DropColumn(
                name: "CoverArtworkId",
                table: "Classifiers");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "Classifiers");

            migrationBuilder.DropColumn(
                name: "DisplayR2Key",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "Artworks");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "Artworks");
        }
    }
}
