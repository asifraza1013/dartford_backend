using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <inheritdoc />
    public partial class AddTitleAndDescriptionToPaymentMilestones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Title column to PaymentMilestones if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PaymentMilestones' AND column_name = 'Title') THEN
                        ALTER TABLE ""PaymentMilestones"" ADD COLUMN ""Title"" character varying(200);
                    END IF;
                END $$;
            ");

            // Add Description column to PaymentMilestones if it doesn't exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'PaymentMilestones' AND column_name = 'Description') THEN
                        ALTER TABLE ""PaymentMilestones"" ADD COLUMN ""Description"" character varying(1000);
                    END IF;
                END $$;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Title",
                table: "PaymentMilestones");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "PaymentMilestones");
        }
    }
}
