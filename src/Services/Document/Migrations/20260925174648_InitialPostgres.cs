using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Document.Migrations
{
    /// <inheritdoc />
    public partial class InitialPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    patient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    doctor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    appointment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "text", nullable: false),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_documents_patient_id",
                table: "documents",
                column: "patient_id");

            migrationBuilder.CreateIndex(
                name: "IX_documents_patient_id_doctor_id_appointment_id",
                table: "documents",
                columns: new[] { "patient_id", "doctor_id", "appointment_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documents");
        }
    }
}
