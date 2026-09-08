using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Adeni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint18MessagingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "messaging");

            migrationBuilder.CreateTable(
                name: "message_threads",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    LastMessageAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    BusinessUnreadCount = table.Column<int>(type: "integer", nullable: false),
                    CustomerUnreadCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_message_threads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "messages",
                schema: "messaging",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ThreadId = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderType = table.Column<int>(type: "integer", nullable: false),
                    SenderAuth0Sub = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_messages_message_threads_ThreadId",
                        column: x => x.ThreadId,
                        principalSchema: "messaging",
                        principalTable: "message_threads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_message_threads_TenantId_CustomerId_BookingId",
                schema: "messaging",
                table: "message_threads",
                columns: new[] { "TenantId", "CustomerId", "BookingId" });

            migrationBuilder.CreateIndex(
                name: "IX_message_threads_TenantId_LastMessageAt",
                schema: "messaging",
                table: "message_threads",
                columns: new[] { "TenantId", "LastMessageAt" });

            migrationBuilder.CreateIndex(
                name: "IX_messages_ThreadId_CreatedAt",
                schema: "messaging",
                table: "messages",
                columns: new[] { "ThreadId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "messages",
                schema: "messaging");

            migrationBuilder.DropTable(
                name: "message_threads",
                schema: "messaging");
        }
    }
}
