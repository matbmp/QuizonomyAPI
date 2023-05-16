namespace QuizonomyAPI.DTO
{
    public class UserRankingGetDTO
    {
        public List<UserExtendedGetDTO> Daily { get; set; }
        public List<UserExtendedGetDTO> Weekly { get; set; }
        public List<UserExtendedGetDTO> Monthly { get; set; }
    }
}
