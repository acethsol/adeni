using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint19TrustAndQuotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PricingType",
                schema: "booking",
                table: "service_offerings",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OwnerReply",
                schema: "booking",
                table: "reviews",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "OwnerReplyAt",
                schema: "booking",
                table: "reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BookingId",
                schema: "booking",
                table: "quote_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAt",
                schema: "booking",
                table: "quote_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoKeysJson",
                schema: "booking",
                table: "quote_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProposedEndAt",
                schema: "booking",
                table: "quote_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ProposedStartAt",
                schema: "booking",
                table: "quote_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuoteNotes",
                schema: "booking",
                table: "quote_requests",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "QuotedAmount",
                schema: "booking",
                table: "quote_requests",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QuotedAt",
                schema: "booking",
                table: "quote_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuotedCurrency",
                schema: "booking",
                table: "quote_requests",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ServiceOfferingId",
                schema: "booking",
                table: "quote_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                schema: "booking",
                table: "quote_requests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "tenant_verification_badges",
                schema: "tenancy",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    BadgeType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    GrantedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    GrantedByAdminId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_verification_badges", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_quote_requests_CustomerId_CreatedAt",
                schema: "booking",
                table: "quote_requests",
                columns: new[] { "CustomerId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_tenant_verification_badges_TenantId_BadgeType",
                schema: "tenancy",
                table: "tenant_verification_badges",
                columns: new[] { "TenantId", "BadgeType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tenant_verification_badges",
                schema: "tenancy");

            migrationBuilder.DropIndex(
                name: "IX_quote_requests_CustomerId_CreatedAt",
                schema: "booking",
                table: "quote_requests");

            migrationBuilder.DropColumn(
                name: "PricingType",
                schema: "booking",
                table: "service_offerings");

            migrationBuilder.DropColumn(
                name: "OwnerReply",
                schema: "booking",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "OwnerReplyAt",
                schema: "booking",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "BookingId",
                schema: "booking",
                table: "quote_requests");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                schema: "booking",
                table: "quote_requests");

            migrationBuilder.DropColumn(
                name: "PhotoKeysJson",
                schema: "booking",
                table: "quote_requests");

            migrationBuilder.DropColumn(
                name: "ProposedEndAt",
                schema: "booking",
                table: "quote_requests");

            migrationBuilder.DropColumn(
                name: "ProposedStartAt",
                schema: "booking",
                table: "quote_requests");

            migrationBuilder.DropColumn(
                name: "QuoteNotes",
                schema: "booking",
                table: "quote_requests");

            migrationBuilder.DropColumn(
                name: "QuotedAmount",
                schema: "booking",
                table: "quote_requests");

            migrationBuilder.DropColumn(
                name: "QuotedAt",
                schema: "booking",
                table: "quote_requests");

            migrationBuilder.DropColumn(
                name: "QuotedCurrency",
                schema: "booking",
                table: "quote_requests");

            migrationBuilder.DropColumn(
                name: "ServiceOfferingId",
                schema: "booking",
                table: "quote_requests");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "booking",
                table: "quote_requests");
        }
    }
}
