using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBusinessProfileMarketId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarketId",
                schema: "tenancy",
                table: "business_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_business_profiles_MarketId",
                schema: "tenancy",
                table: "business_profiles",
                column: "MarketId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_business_profiles_MarketId",
                schema: "tenancy",
                table: "business_profiles");

            migrationBuilder.DropColumn(
                name: "MarketId",
                schema: "tenancy",
                table: "business_profiles");
        }
    }
}
