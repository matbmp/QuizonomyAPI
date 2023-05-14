namespace QuizonomyAPI.Models
{
    public class Session
    {

        public long Id { get; set; }
        public string Key { get; set; } = null!;
        public long UserId { get; set; }
        public virtual User User { get; set; } = null!;
    }
}
