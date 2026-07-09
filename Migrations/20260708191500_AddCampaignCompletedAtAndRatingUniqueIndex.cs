using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignCompletedAtAndRatingUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ratings_CampaignId",
                table: "Ratings");

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_CampaignId_RaterId",
                table: "Ratings",
                columns: new[] { "CampaignId", "RaterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_RateeId_RateeUserType",
                table: "Ratings",
                columns: new[] { "RateeId", "RateeUserType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Ratings_CampaignId_RaterId",
                table: "Ratings");

            migrationBuilder.DropIndex(
                name: "IX_Ratings_RateeId_RateeUserType",
                table: "Ratings");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Campaigns");

            migrationBuilder.CreateIndex(
                name: "IX_Ratings_CampaignId",
                table: "Ratings",
                column: "CampaignId");
        }
    }
}
