namespace QuizonomyAPI.DTO
{
    public class UserExtendedGetDTO
    {
        public long Id { get; set; }
        public string Username { get; set; } = null!;
        public long DailyQuoins { get; set; }
        public long WeeklyQuoins { get; set; }
        public long MonthlyQuoins { get; set; }
        public long DailyCount { get; set; }
    }
}
