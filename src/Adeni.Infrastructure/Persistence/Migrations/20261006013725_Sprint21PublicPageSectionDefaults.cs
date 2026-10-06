using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint21PublicPageSectionDefaults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "PublicPageShowVisit",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "PublicPageShowServices",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "PublicPageShowReviews",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<bool>(
                name: "PublicPageShowAbout",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            // First migration briefly defaulted these to false; restore show-all for existing rows.
            migrationBuilder.Sql(
                """
                UPDATE tenancy.business_profiles
                SET "PublicPageShowAbout" = TRUE,
                    "PublicPageShowServices" = TRUE,
                    "PublicPageShowReviews" = TRUE,
                    "PublicPageShowVisit" = TRUE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "PublicPageShowVisit",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "PublicPageShowServices",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "PublicPageShowReviews",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<bool>(
                name: "PublicPageShowAbout",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
        }
    }
}
