using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogMarkets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.CreateTable(
                name: "markets",
                schema: "catalog",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DefaultLat = table.Column<double>(type: "double precision", nullable: false),
                    DefaultLng = table.Column<double>(type: "double precision", nullable: false),
                    LanguagesJson = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    IsLive = table.Column<bool>(type: "boolean", nullable: false),
                    LaunchNote = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_markets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_markets_IsLive",
                schema: "catalog",
                table: "markets",
                column: "IsLive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "markets",
                schema: "catalog");
        }
    }
}
