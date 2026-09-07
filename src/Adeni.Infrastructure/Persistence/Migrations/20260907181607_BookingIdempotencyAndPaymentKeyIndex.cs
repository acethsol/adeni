using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BookingIdempotencyAndPaymentKeyIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "booking",
                table: "bookings",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_IdempotencyKey",
                schema: "payments",
                table: "payment_intents",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_IdempotencyKey",
                schema: "booking",
                table: "bookings",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_intents_IdempotencyKey",
                schema: "payments",
                table: "payment_intents");

            migrationBuilder.DropIndex(
                name: "IX_bookings_IdempotencyKey",
                schema: "booking",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "booking",
                table: "bookings");
        }
    }
}
