using QuizonomyAPI.DTO;
using System.ComponentModel.DataAnnotations;

namespace QuizonomyAPI.Models
{
    public class Question
    {
        public long Id { get; set; }
        public string QuestionText { get; set; } = null!;
        public string AnswersJsonArray { get; set; } = null!;
        public uint CorrectAnswer { get; set; }
        public string? ImageUrl { get; set; }
    }
}
