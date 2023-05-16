namespace QuizonomyAPI.DTO
{
    public class SessionDTO
    {
        public string Key { get; set; } = null!;
        public UserExtendedGetDTO User { get; set; } = null!;
    }
}
