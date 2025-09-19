using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveTwitterAddYouTube : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Twitter",
                table: "Influencers");

            migrationBuilder.RenameColumn(
                name: "TwitterFollower",
                table: "Influencers",
                newName: "YouTubeFollower");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "YouTubeFollower",
                table: "Influencers",
                newName: "TwitterFollower");

            migrationBuilder.AddColumn<string>(
                name: "Twitter",
                table: "Influencers",
                type: "text",
                nullable: true);
        }
    }
}
