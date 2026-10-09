using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Notification.Migrations
{
    /// <inheritdoc />
    public partial class IndexNotificationsByUserAndDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_created_at",
                table: "notifications",
                columns: new[] { "user_id", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_user_id_created_at",
                table: "notifications");
        }
    }
}
