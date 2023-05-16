using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizonomyAPI.Models.migration
{
    public partial class addAttemptCount : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "AttemptCount",
                table: "Quizzes",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "Quizzes");
        }
    }
}
