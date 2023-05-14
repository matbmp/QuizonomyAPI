using System.ComponentModel.DataAnnotations;

namespace QuizonomyAPI.DTO
{
    public class QuestionPostDTO
    {
        [Required]
        public string QuestionText { get; set; } = null!;
        [Required]
        public string? AnswersJsonArray { get; set; } = null!;
        [Range(0,3)]
        public uint CorrectAnswer { get; set; }
        public string? ImageUrl { get; set; }
    }

}
