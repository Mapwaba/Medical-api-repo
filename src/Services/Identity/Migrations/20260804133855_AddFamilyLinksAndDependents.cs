using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddFamilyLinksAndDependents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dependents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    guardian_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "text", nullable: false),
                    last_name = table.Column<string>(type: "text", nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    gender = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dependents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "family_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation_type = table.Column<string>(type: "text", nullable: false),
                    requester_is_parent = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    responded_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    responded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_family_links", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_dependents_guardian_user_id",
                table: "dependents",
                column: "guardian_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_family_links_recipient_user_id_status",
                table: "family_links",
                columns: new[] { "recipient_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_family_links_requester_user_id_status",
                table: "family_links",
                columns: new[] { "requester_user_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dependents");

            migrationBuilder.DropTable(
                name: "family_links");
        }
    }
}
