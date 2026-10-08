using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint24StaffOps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "booking",
                table: "staff_members",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                schema: "booking",
                table: "staff_members",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RoleKey",
                schema: "booking",
                table: "staff_members",
                type: "character varying(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "other");

            // Backfill identity from existing display names (Sprint 23 roster).
            migrationBuilder.Sql(
                """
                UPDATE booking.staff_members
                SET
                  "FirstName" = CASE
                    WHEN strpos("DisplayName", ' ') > 0 THEN split_part("DisplayName", ' ', 1)
                    ELSE "DisplayName"
                  END,
                  "LastName" = CASE
                    WHEN strpos("DisplayName", ' ') > 0 THEN trim(substring("DisplayName" from strpos("DisplayName", ' ') + 1))
                    ELSE '.'
                  END
                WHERE coalesce("FirstName", '') = '';
                """);

            migrationBuilder.CreateTable(
                name: "staff_leave",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_leave", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_leave_staff_members_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalSchema: "booking",
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staff_weekly_availability",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    StaffMemberId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    OpenTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    CloseTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_staff_weekly_availability", x => x.Id);
                    table.ForeignKey(
                        name: "FK_staff_weekly_availability_staff_members_StaffMemberId",
                        column: x => x.StaffMemberId,
                        principalSchema: "booking",
                        principalTable: "staff_members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_staff_leave_StaffMemberId",
                schema: "booking",
                table: "staff_leave",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_leave_TenantId_StaffMemberId_StartAt",
                schema: "booking",
                table: "staff_leave",
                columns: new[] { "TenantId", "StaffMemberId", "StartAt" });

            migrationBuilder.CreateIndex(
                name: "IX_staff_weekly_availability_StaffMemberId",
                schema: "booking",
                table: "staff_weekly_availability",
                column: "StaffMemberId");

            migrationBuilder.CreateIndex(
                name: "IX_staff_weekly_availability_TenantId_StaffMemberId_DayOfWeek",
                schema: "booking",
                table: "staff_weekly_availability",
                columns: new[] { "TenantId", "StaffMemberId", "DayOfWeek" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "staff_leave",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "staff_weekly_availability",
                schema: "booking");

            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "booking",
                table: "staff_members");

            migrationBuilder.DropColumn(
                name: "LastName",
                schema: "booking",
                table: "staff_members");

            migrationBuilder.DropColumn(
                name: "RoleKey",
                schema: "booking",
                table: "staff_members");
        }
    }
}
