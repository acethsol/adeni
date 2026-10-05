using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class WellnessTaxonomy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "business_profile_categories",
                schema: "tenancy",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategorySlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_business_profile_categories", x => new { x.TenantId, x.CategorySlug });
                    table.ForeignKey(
                        name: "FK_business_profile_categories_business_profiles_TenantId",
                        column: x => x.TenantId,
                        principalSchema: "tenancy",
                        principalTable: "business_profiles",
                        principalColumn: "TenantId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddColumn<int>(
                name: "BookingDeliveryType",
                schema: "booking",
                table: "service_offerings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CatalogServiceId",
                schema: "booking",
                table: "service_offerings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategorySlug",
                schema: "booking",
                table: "service_offerings",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.Sql(
                """
                INSERT INTO tenancy.business_profile_categories ("TenantId", "CategorySlug", "IsPrimary")
                SELECT bp."TenantId", LOWER(bp."CategorySlug"), TRUE
                FROM tenancy.business_profiles bp
                WHERE bp."CategorySlug" <> ''
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "business_profile_categories",
                schema: "tenancy");

            migrationBuilder.DropColumn(
                name: "BookingDeliveryType",
                schema: "booking",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "CatalogServiceId",
                schema: "booking",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "CategorySlug",
                schema: "booking",
                table: "service_offerings");
        }
    }
}
