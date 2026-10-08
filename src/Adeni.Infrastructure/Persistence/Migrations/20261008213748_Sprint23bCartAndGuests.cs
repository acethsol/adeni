using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint23bCartAndGuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAddOn",
                schema: "booking",
                table: "service_offerings",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "GuestCount",
                schema: "booking",
                table: "bookings",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateTable(
                name: "booking_guests",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_guests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_guests_bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "booking",
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "booking_lines",
                schema: "booking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceOfferingId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    PriceAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsAddOn = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_lines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_booking_lines_bookings_BookingId",
                        column: x => x.BookingId,
                        principalSchema: "booking",
                        principalTable: "bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_booking_lines_service_offerings_ServiceOfferingId",
                        column: x => x.ServiceOfferingId,
                        principalSchema: "booking",
                        principalTable: "service_offerings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_booking_guests_BookingId_SortOrder",
                schema: "booking",
                table: "booking_guests",
                columns: new[] { "BookingId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_lines_BookingId_SortOrder",
                schema: "booking",
                table: "booking_lines",
                columns: new[] { "BookingId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_booking_lines_ServiceOfferingId",
                schema: "booking",
                table: "booking_lines",
                column: "ServiceOfferingId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "booking_guests",
                schema: "booking");

            migrationBuilder.DropTable(
                name: "booking_lines",
                schema: "booking");

            migrationBuilder.DropColumn(
                name: "IsAddOn",
                schema: "booking",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "GuestCount",
                schema: "booking",
                table: "bookings");
        }
    }
}
