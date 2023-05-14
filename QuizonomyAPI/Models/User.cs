namespace QuizonomyAPI.Models
{
    public class User
    {
        public long Id { get; set; }
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
        public long DailyQuoins { get; set; }
        public long WeeklyQuoins { get; set; }
        public long MonthlyQuoins { get; set; }

        public DateTime DailyQuizDate { get; set; }
        public long DailyCount { get; set; }
        public long DailyQuizId { get; set; }
    }
}
