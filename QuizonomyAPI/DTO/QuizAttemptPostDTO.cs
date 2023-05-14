namespace QuizonomyAPI.DTO
{
    public class QuizAttemptPostDTO
    {
        public uint CorrectCount { get; set; }
        public DateTime Time { get; set; }
        public long QuizId { get; set; }
    }
}
