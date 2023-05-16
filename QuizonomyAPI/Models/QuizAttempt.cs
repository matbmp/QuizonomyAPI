namespace QuizonomyAPI.Models
{
    public class QuizAttempt
    {
        public long Id { get; set; }
        public uint CorrectCount { get; set; }
        public float TimeMilliseconds { get; set; }
        public long UserId { get; set; }
        public virtual User User { get; set; } = null!;
        public long QuizId { get; set; }
        public virtual Quiz Quiz { get; set; } = null!;
    }
}
