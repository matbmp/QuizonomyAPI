namespace QuizonomyAPI.DTO
{
    public class QuizAttemptPostDTO
    {
        public uint CorrectCount { get; set; }
        public long TimeMilliseconds { get; set; }
        public long QuizId { get; set; }
    }
}
