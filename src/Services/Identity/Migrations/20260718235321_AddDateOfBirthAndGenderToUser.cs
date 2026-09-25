using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LandaDoc.Identity.Migrations
{
    /// <inheritdoc />
    public partial class AddDateOfBirthAndGenderToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "date_of_birth",
                table: "users",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "gender",
                table: "users",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "date_of_birth",
                table: "users");

            migrationBuilder.DropColumn(
                name: "gender",
                table: "users");
        }
    }
}
