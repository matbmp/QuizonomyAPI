using Microsoft.EntityFrameworkCore;
using QuizonomyAPI.Models;
using System.Runtime.CompilerServices;

namespace QuizonomyAPI
{
    public static class IQueryableExtensions
    {
        public static IQueryable<User> ByUsername(this IQueryable<User> query, string username)
        {
            return query.Where(u => u.Username == username);
        }

        public static IQueryable<Quiz> WithAuthorAndQuestions(this IQueryable<Quiz> query)
        {
            return query.Include(q => q.Author).Include(q => q.Questions);
        }

        public static IQueryable<T> SkipTake<T>(this IQueryable<T> query, int skip, int take)
        {
            return query.Skip(skip).Take(take);
        }

        public static IQueryable<Quiz> ById(this IQueryable<Quiz> query, long id)
        {
            return query.Where(q => q.Id == id);
        }

        public static IQueryable<QuizAttempt> ByQuizIdAndUserId(this IQueryable<QuizAttempt> query, long quizId, long userId)
        {
            return query.Where(qa => qa.UserId == userId && qa.QuizId == quizId);
        }

        public static IQueryable<Session> ByKeyIdAndUserId(this IQueryable<Session> query, string key, long userId)
        {
            return query.Where(s => s.UserId == userId && s.Key == key);
        }
    }
}
