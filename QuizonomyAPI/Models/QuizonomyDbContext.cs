using Microsoft.EntityFrameworkCore;

namespace QuizonomyAPI.Models
{
    public class QuizonomyDbContext:DbContext
    {
        public QuizonomyDbContext(DbContextOptions<QuizonomyDbContext> options):base(options)
        {

        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("pg_trgm");
            
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<QuizAttempt> QuizAttempts { get; set; }
        public DbSet<Session> Sessions { get; set; }
        }
}
