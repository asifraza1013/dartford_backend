using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class AddReminderSentAtToScheduledPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReminderSentAt",
                table: "ScheduledPosts",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReminderSentAt",
                table: "ScheduledPosts");
        }
    }
}
