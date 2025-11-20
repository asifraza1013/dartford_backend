using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class CampaignBookingWorkflowEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AboutProject",
                table: "Campaigns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "ContentFiles",
                table: "Campaigns",
                type: "text[]",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContractSignedAt",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "GeneratedContractPdfPath",
                table: "Campaigns",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InfluencerAcceptedAt",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PaymentCompletedAt",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProjectName",
                table: "Campaigns",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SignedContractPdfPath",
                table: "Campaigns",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AboutProject",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "ContentFiles",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "ContractSignedAt",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "GeneratedContractPdfPath",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "InfluencerAcceptedAt",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "PaymentCompletedAt",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "ProjectName",
                table: "Campaigns");

            migrationBuilder.DropColumn(
                name: "SignedContractPdfPath",
                table: "Campaigns");

            migrationBuilder.AlterColumn<string>(
                name: "Password",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}
