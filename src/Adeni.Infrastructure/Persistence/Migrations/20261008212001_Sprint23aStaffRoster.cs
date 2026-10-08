using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint23aStaffRoster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "StaffMemberId",
                schema: "booking",
                table: "bookings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "staff_members",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Bio = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    AvatarImageKey = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_members", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "staff_service_links",
                schema: "booking",
                columns: table => new
                {
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceOfferingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_service_links", x => new { x.StaffMemberId, x.ServiceOfferingId });
                    table.ForeignKey(
                        name: "FK_staff_service_links_service_offerings_ServiceOfferingId",
                        column: x => x.ServiceOfferingId,
                        principalSchema: "booking",
                        principalTable: "service_offerings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_staff_service_links_staff_members_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalSchema: "booking",
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bookings_StaffMemberId",
                schema: "booking",
                table: "bookings",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_TenantId_StaffMemberId_StartAt",
                schema: "booking",
                table: "bookings",
                columns: new[] { "TenantId", "StaffMemberId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_members_TenantId_IsActive_SortOrder",
                schema: "booking",
                table: "staff_members",
                columns: new[] { "TenantId", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_service_links_ServiceOfferingId",
                schema: "booking",
                table: "staff_service_links",
                column: "ServiceOfferingId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_service_links_TenantId_ServiceOfferingId",
                schema: "booking",
                table: "staff_service_links",
                columns: new[] { "TenantId", "ServiceOfferingId" });

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_staff_members_StaffMemberId",
                schema: "booking",
                table: "bookings",
                column: "StaffMemberId",
                principalSchema: "booking",
                principalTable: "staff_members",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bookings_staff_members_StaffMemberId",
                schema: "booking",
                table: "bookings");

            migrationBuilder.DropTable(
                name: "staff_service_links",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "staff_members",
                schema: "booking");

            migrationBuilder.DropIndex(
                name: "IX_bookings_StaffMemberId",
                schema: "booking",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_bookings_TenantId_StaffMemberId_StartAt",
                schema: "booking",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "StaffMemberId",
                schema: "booking",
                table: "bookings");
        }
    }
}
