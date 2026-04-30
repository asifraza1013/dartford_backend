using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace inflan_api.Migrations
{
    /// <summary>
    /// Pure data fix. The brand goals form (frontend Goals.tsx) used to call
    /// FormData.append("brandSector", data.category[0]) with no validation;
    /// when category was empty, JS stringified `undefined` to the literal
    /// 9-char string "undefined" before posting. The backend stored it
    /// verbatim, so brand profile pages started rendering "undefined" as
    /// the industry sector.
    ///
    /// This migration NULLs out any "undefined" / "null" string left in
    /// BrandSector or BrandCategory (case-insensitive, trimmed). Idempotent
    /// and safe to re-run; no schema change.
    /// </summary>
    public partial class BackfillUndefinedBrandFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE ""Users""
                SET ""BrandSector"" = NULL
                WHERE ""BrandSector"" IS NOT NULL
                  AND LOWER(TRIM(""BrandSector"")) IN ('undefined', 'null', '');
            ");

            migrationBuilder.Sql(@"
                UPDATE ""Users""
                SET ""BrandCategory"" = NULL
                WHERE ""BrandCategory"" IS NOT NULL
                  AND LOWER(TRIM(""BrandCategory"")) IN ('undefined', 'null', '');
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Pure data scrub — we don't keep an audit trail to revert it.
        }
    }
}
