using QuizonomyAPI.Models;

namespace QuizonomyAPI.DTO
{
    public class QuizPostDTO
    {
        public string Name { get; set; } = null!;
        public virtual long AuthorId { get; set; }
        public virtual ICollection<QuestionPostDTO> Questions { get; set; } = new List<QuestionPostDTO>();
    }
}
