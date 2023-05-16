using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QuizonomyAPI.Models.migration
{
    public partial class addBestAttemptScore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "BestAttemptScore",
                table: "Quizzes",
                type: "real",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BestAttemptScore",
                table: "Quizzes");
        }
    }
}
