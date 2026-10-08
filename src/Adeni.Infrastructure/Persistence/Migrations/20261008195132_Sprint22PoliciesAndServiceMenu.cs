using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint22PoliciesAndServiceMenu : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MenuGroupId",
                schema: "booking",
                table: "service_offerings",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                schema: "booking",
                table: "service_offerings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PolicyBookingText",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyCancellationText",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyPaymentText",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PolicyTermsText",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(8000)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "PublicPageShowPolicies",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequirePolicyAcceptance",
                schema: "tenancy",
                table: "business_profiles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "service_menu_groups",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_menu_groups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_service_offerings_MenuGroupId",
                schema: "booking",
                table: "service_offerings",
                column: "MenuGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_service_offerings_TenantId_MenuGroupId_SortOrder",
                schema: "booking",
                table: "service_offerings",
                columns: new[] { "TenantId", "MenuGroupId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_service_menu_groups_TenantId_SortOrder",
                schema: "booking",
                table: "service_menu_groups",
                columns: new[] { "TenantId", "SortOrder" });

            migrationBuilder.AddForeignKey(
                name: "FK_service_offerings_service_menu_groups_MenuGroupId",
                schema: "booking",
                table: "service_offerings",
                column: "MenuGroupId",
                principalSchema: "booking",
                principalTable: "service_menu_groups",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_service_offerings_service_menu_groups_MenuGroupId",
                schema: "booking",
                table: "service_offerings");

            migrationBuilder.DropTable(
                name: "service_menu_groups",
                schema: "booking");

            migrationBuilder.DropIndex(
                name: "IX_service_offerings_MenuGroupId",
                schema: "booking",
                table: "service_offerings");

            migrationBuilder.DropIndex(
                name: "IX_service_offerings_TenantId_MenuGroupId_SortOrder",
                schema: "booking",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "MenuGroupId",
                schema: "booking",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                schema: "booking",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "PolicyBookingText",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "PolicyCancellationText",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "PolicyPaymentText",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "PolicyTermsText",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "PublicPageShowPolicies",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "RequirePolicyAcceptance",
                schema: "tenancy",
                table: "business_profiles");
        }
    }
}
