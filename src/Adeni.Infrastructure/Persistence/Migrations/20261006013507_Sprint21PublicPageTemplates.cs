using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint21PublicPageTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoImageKey",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicPageAccentColor",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublicPageShowAbout",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublicPageShowReviews",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublicPageShowServices",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublicPageShowVisit",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "PublicPageTemplateId",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "studio");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoImageKey",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "PublicPageAccentColor",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "PublicPageShowAbout",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "PublicPageShowReviews",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "PublicPageShowServices",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "PublicPageShowVisit",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "PublicPageTemplateId",
                schema: "tenancy",
                table: "business_profiles");
        }
    }
}
