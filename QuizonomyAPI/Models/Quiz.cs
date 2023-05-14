namespace QuizonomyAPI.Models
{
    public class Quiz
    {
        public long Id { get; set; }
        public string Name { get; set; } = null!;

        public long AuthorId { get; set; }
        public virtual User Author { get; set; } = null!;
        public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
    }
}
