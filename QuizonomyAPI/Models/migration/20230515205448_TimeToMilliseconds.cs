using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizonomyAPI.Models.migration
{
    public partial class TimeToMilliseconds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Time",
                table: "QuizAttempts");

            migrationBuilder.AddColumn<float>(
                name: "TimeMilliseconds",
                table: "QuizAttempts",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TimeMilliseconds",
                table: "QuizAttempts");

            migrationBuilder.AddColumn<DateTime>(
                name: "Time",
                table: "QuizAttempts",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }
    }
}
