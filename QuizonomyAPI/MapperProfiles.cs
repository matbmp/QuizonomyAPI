using AutoMapper;
using QuizonomyAPI.DTO;
using QuizonomyAPI.Models;

namespace QuizonomyAPI
{
    public class MapperProfiles : Profile
    {
        
        public MapperProfiles() {
            
            CreateMap<Question, QuestionPostDTO>();
            CreateMap<QuestionPostDTO, Question>();

            CreateMap<QuizPostDTO, Quiz>();
            CreateMap<Quiz, QuizGetDTO>();

            CreateMap<UserPostDTO, User>().ForMember(dest => dest.Password, opt => opt.MapFrom(src => BCrypt.Net.BCrypt.HashPassword(src.Password)));
            CreateMap<User, UserGetDTO>();
            CreateMap<User, UserExtendedGetDTO>();

            CreateMap<Session, SessionDTO>();
        }
    }
}
