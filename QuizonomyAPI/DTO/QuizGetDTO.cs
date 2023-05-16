using QuizonomyAPI.Models;

namespace QuizonomyAPI.DTO
{
    public class QuizGetDTO
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public long AttemptCount { get; set; }
        public float? BestAttemptScore { get; set; }
        public virtual UserGetDTO Author { get; set; } = null!;
        public virtual ICollection<QuestionPostDTO> Questions { get; set; } = new List<QuestionPostDTO>();
    }
}
