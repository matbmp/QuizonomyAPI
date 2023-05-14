using QuizonomyAPI.Models;

namespace QuizonomyAPI.DTO
{
    public class QuizGetDTO
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;
        public virtual UserGetDTO Author { get; set; } = null!;
        public virtual ICollection<QuestionPostDTO> Questions { get; set; } = new List<QuestionPostDTO>();
    }
}
