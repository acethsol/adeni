using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint17PaymentsOrchestration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CallbackUrl",
                schema: "payments",
                table: "payment_intents",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerEmail",
                schema: "payments",
                table: "payment_intents",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                schema: "payments",
                table: "payment_intents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                schema: "payments",
                table: "payment_intents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PlatformFeeAmount",
                schema: "payments",
                table: "payment_intents",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "payments",
                table: "payment_intents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DepositPercent",
                schema: "tenancy",
                table: "business_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_payment_intents_ProviderReference",
                schema: "payments",
                table: "payment_intents",
                column: "ProviderReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payment_intents_ProviderReference",
                schema: "payments",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "CallbackUrl",
                schema: "payments",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "CustomerEmail",
                schema: "payments",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "Description",
                schema: "payments",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                schema: "payments",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "PlatformFeeAmount",
                schema: "payments",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "Type",
                schema: "payments",
                table: "payment_intents");

            migrationBuilder.DropColumn(
                name: "DepositPercent",
                schema: "tenancy",
                table: "business_profiles");
        }
    }
}
