using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Payment.Migrations
{
    /// <inheritdoc />
    public partial class AddMobileMoneyAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mobile_money_attempts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    provider_transaction_id = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    @operator = table.Column<string>(name: "operator", type: "text", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    outcome = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mobile_money_attempts", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_mobile_money_attempts_reference",
                table: "mobile_money_attempts",
                column: "reference",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_mobile_money_attempts_resolved_at",
                table: "mobile_money_attempts",
                column: "resolved_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mobile_money_attempts");
        }
    }
}
