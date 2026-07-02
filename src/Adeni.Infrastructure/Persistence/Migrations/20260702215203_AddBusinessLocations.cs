using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessLocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_locations",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    MarketId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    AddressLine = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Area = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Latitude = table.Column<double>(type: "double precision", nullable: true),
                    Longitude = table.Column<double>(type: "double precision", nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_locations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_business_locations_tenants_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenancy",
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO tenancy.business_locations (
                    "Id", "TenantId", "Slug", "Name", "MarketId", "AddressLine", "Area",
                    "Latitude", "Longitude", "TimeZoneId", "IsPrimary", "IsActive", "CreatedAt", "UpdatedAt")
                SELECT
                    gen_random_uuid(),
                    bp."TenantId",
                    bp."Slug",
                    COALESCE(NULLIF(t."Name", ''), bp."Area"),
                    COALESCE(NULLIF(bp."MarketId", ''), 'lagos'),
                    bp."AddressLine",
                    bp."Area",
                    bp."Latitude",
                    bp."Longitude",
                    bp."TimeZoneId",
                    TRUE,
                    TRUE,
                    bp."UpdatedAt",
                    bp."UpdatedAt"
                FROM tenancy.business_profiles bp
                INNER JOIN tenancy.tenants t ON t."Id" = bp."TenantId"
                WHERE bp."Slug" <> '';
                """);

            migrationBuilder.DropIndex(
                name: "IX_business_profiles_MarketId",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropIndex(
                name: "IX_business_profiles_Slug",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "AddressLine",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "Area",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "Latitude",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "Longitude",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "MarketId",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "TimeZoneId",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.CreateIndex(
                name: "IX_business_locations_MarketId",
                schema: "tenancy",
                table: "business_locations",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "IX_business_locations_Slug",
                schema: "tenancy",
                table: "business_locations",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_business_locations_TenantId_IsPrimary",
                schema: "tenancy",
                table: "business_locations",
                columns: new[] { "TenantId", "IsPrimary" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AddressLine",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Area",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(120)",
                maxLength: 120,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                schema: "tenancy",
                table: "business_profiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                schema: "tenancy",
                table: "business_profiles",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MarketId",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TimeZoneId",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE tenancy.business_profiles bp
                SET
                    "Slug" = loc."Slug",
                    "MarketId" = loc."MarketId",
                    "AddressLine" = loc."AddressLine",
                    "Area" = loc."Area",
                    "Latitude" = loc."Latitude",
                    "Longitude" = loc."Longitude",
                    "TimeZoneId" = loc."TimeZoneId"
                FROM tenancy.business_locations loc
                WHERE loc."TenantId" = bp."TenantId"
                  AND loc."IsPrimary" = TRUE
                  AND loc."IsActive" = TRUE;
                """);

            migrationBuilder.DropTable(
                name: "business_locations",
                schema: "tenancy");

            migrationBuilder.CreateIndex(
                name: "IX_business_profiles_MarketId",
                schema: "tenancy",
                table: "business_profiles",
                column: "MarketId");

            migrationBuilder.CreateIndex(
                name: "IX_business_profiles_Slug",
                schema: "tenancy",
                table: "business_profiles",
                column: "Slug",
                unique: true);
        }
    }
}
