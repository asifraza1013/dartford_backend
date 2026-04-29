using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class AddMilestoneReminderFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OverdueNoticeSentAt",
                table: "PaymentMilestones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Reminder1DaySentAt",
                table: "PaymentMilestones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Reminder3DaysSentAt",
                table: "PaymentMilestones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "Reminder7DaysSentAt",
                table: "PaymentMilestones",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OverdueNoticeSentAt",
                table: "PaymentMilestones");

            migrationBuilder.DropColumn(
                name: "Reminder1DaySentAt",
                table: "PaymentMilestones");

            migrationBuilder.DropColumn(
                name: "Reminder3DaysSentAt",
                table: "PaymentMilestones");

            migrationBuilder.DropColumn(
                name: "Reminder7DaysSentAt",
                table: "PaymentMilestones");
        }
    }
}
