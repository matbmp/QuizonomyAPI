using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuizonomyAPI.Models;
using QuizonomyAPI.DTO;
using AutoMapper;
using QuizonomyAPI;
using QuizonomyAPI.Services;
using System.Reflection;
using QuizonomyAPI.Middleware;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Security.Cryptography;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

AuthSettings authSettings = new AuthSettings();
builder.Configuration.GetSection("Auth").Bind(authSettings);
authSettings.CookieOptions = new CookieOptions()
{
    MaxAge = TimeSpan.FromHours(1),
};
builder.Services.AddSingleton(authSettings);

builder.Services.AddValidatorsFromAssemblyContaining<UserPostDTOValidator>();
builder.Services.AddAutoMapper(Assembly.GetExecutingAssembly());
builder.Services.AddDbContext<QuizonomyDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("QuizonomyConnection")
    ));
builder.Services.AddScoped<TokenService>();


builder.Services.AddTransient<CustomAuthenticationHandler>();
builder.Services.AddAuthentication(authSettings.AuthenticationScheme)
        .AddScheme<AuthenticationSchemeOptions, CustomAuthenticationHandler>(authSettings.AuthenticationScheme, null);
builder.Services.AddAuthorization(options =>
{
    var AnyAuthorizationPolicyBuilder = new AuthorizationPolicyBuilder(authSettings.AuthenticationScheme);
    AnyAuthorizationPolicyBuilder = AnyAuthorizationPolicyBuilder.RequireAuthenticatedUser();
    options.DefaultPolicy = AnyAuthorizationPolicyBuilder.Build();
});

builder.Services.AddCors(setup =>
{
    setup.AddDefaultPolicy(opts =>
    {
        opts.AllowAnyHeader();
        opts.AllowAnyMethod();
        opts.AllowAnyOrigin();
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();


app.MapGet("/quiz/{id}", async (long id, QuizonomyDbContext db, IMapper mapper) => {
    var quiz = await db.Quizzes.Where(q => q.Id == id).Include(q => q.Author).Include(q => q.Questions).FirstOrDefaultAsync();
    if (quiz is null) return Results.NotFound();
    return Results.Ok(mapper.Map<QuizGetDTO>(quiz));
    });
app.MapGet("/quiz", async ([FromQuery] string searchQuery, [FromQuery] int skip, [FromQuery] int take, [FromServices]QuizonomyDbContext db, [FromServices]IMapper mapper) =>
{
    List<Quiz> quizzes;
    if(searchQuery != null)
    {
        quizzes = await db.Quizzes.OrderBy(q => -EF.Functions.TrigramsSimilarity(q.Name, searchQuery))
        .Skip(skip).Take(take).Include(q => q.Author).Include(q => q.Questions).ToListAsync();
    }
    else
    {
        quizzes = await db.Quizzes.OrderBy(q => -q.AttemptCount).Skip(skip).Take(take)
        .Include(q => q.Author).Include(q => q.Questions).ToListAsync();
    }
    return mapper.Map<ICollection<QuizGetDTO>>(quizzes);
});
app.MapGet("/quiz/random", async ([FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper) =>
{
    var quizzes = await db.Quizzes.OrderBy(q => Guid.NewGuid()).Take(1).Include(q => q.Author).Include(q => q.Questions).ToListAsync();
    return mapper.Map<ICollection<QuizGetDTO>>(quizzes);
});
app.MapGet("/quiz/popular", async ([FromQuery] int take, [FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper) =>
{
    var quizzes = await db.Quizzes.OrderBy(q => -q.AttemptCount).Take(take).Include(q => q.Author).Include(q => q.Questions).ToListAsync();
    return mapper.Map<ICollection<QuizGetDTO>>(quizzes);
});
app.MapGet("/quiz/daily", [Authorize] async ([FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper, HttpContext context) =>
{
    if (context.User.Identity is not ClaimsIdentity identity) return Results.Problem();
    if (identity.FindFirst(ClaimTypes.Name) is not Claim nameClaim) return Results.Problem();
    if (db.Users.Where(u => u.Username == nameClaim.Value).FirstOrDefault() is not User user) return Results.Problem();

    if(user.DailyQuizDate.Date != DateTimeOffset.Now.Date)
    {
        user.DailyCount = 3;
        user.DailyQuizDate = DateTimeOffset.Now.Date;
    }
    if (user.DailyCount <= 0) return Results.BadRequest();
    var quiz = await db.Quizzes.OrderBy(q => Guid.NewGuid()).FirstOrDefaultAsync();
    if(quiz is null) return Results.NotFound();
    user.DailyQuizId = quiz.Id;
    user.DailyCount--;
    await db.SaveChangesAsync();
    return Results.Ok(quiz.Id);
});
app.MapPost("/quiz/submit", [Authorize] async ([FromBody]QuizAttemptPostDTO attemptDTO, [FromServices] QuizonomyDbContext db, HttpContext context) =>
{
    var identity = context.User.Identity as ClaimsIdentity;
    if (identity is null) return Results.Problem();
    var nameClaim = identity.FindFirst(ClaimTypes.Name);
    if (nameClaim is null) return Results.Problem();
    var user = db.Users.Where(u => u.Username == nameClaim.Value).FirstOrDefault();
    if (user is null) return Results.Problem();

    if(user.DailyQuizId == attemptDTO.QuizId)
    {
        var quiz = await db.Quizzes.Where(q => q.Id == attemptDTO.QuizId).Include(q => q.Questions).FirstOrDefaultAsync();
        if (quiz is null) return Results.Problem();
        user.DailyQuizId = -1;

        float correctCount = attemptDTO.CorrectCount;
        int questionCount = quiz.Questions.Count();
        int quoins = (int) ((correctCount / questionCount) * (100 * MathF.Sqrt(questionCount)));
        user.DailyQuoins += quoins;
        user.WeeklyQuoins += quoins;
        user.MonthlyQuoins += quoins;
    }
    var attempt = await db.QuizAttempts.Where(qa => qa.UserId == user.Id && qa.QuizId == attemptDTO.QuizId).FirstOrDefaultAsync();
    if(attempt is not null)
    {
        float oldRatio = 1000*attempt.CorrectCount / (float)(attempt.TimeMilliseconds);
        float newRatio = 1000*attemptDTO.CorrectCount / (float)(attemptDTO.TimeMilliseconds);
        if (newRatio > oldRatio)
        {
            attempt.TimeMilliseconds = attemptDTO.TimeMilliseconds;
            attempt.CorrectCount = attemptDTO.CorrectCount;
        }
    }
    else
    {
        attempt = new QuizAttempt()
        {
            CorrectCount = attemptDTO.CorrectCount,
            QuizId = attemptDTO.QuizId,
            TimeMilliseconds = attemptDTO.TimeMilliseconds,
            UserId = user.Id,
        };
        db.QuizAttempts.Add(attempt);
    }

    float attemptScore = 1000 * attemptDTO.CorrectCount / (float)(attemptDTO.TimeMilliseconds);
    var quiz2 = db.Quizzes.Where(q => q.Id == attemptDTO.QuizId).FirstOrDefault();
    if(quiz2 is not null)
    {
        quiz2.AttemptCount++;
        if (!quiz2.BestAttemptScore.HasValue)
        {
            quiz2.BestAttemptScore = attemptScore;
        }
        else if (attemptScore > quiz2.BestAttemptScore)
        {
            quiz2.BestAttemptScore = attemptScore;
        }
    }

    await db.SaveChangesAsync();
    return Results.Ok();
});
app.MapPost("/quiz", async ([FromBody] QuizPostDTO quizDTO, [FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper) =>
{
    var quiz = mapper.Map<Quiz>(quizDTO);
    db.Quizzes.Add(quiz);
    await db.SaveChangesAsync();

    return Results.Created($"/question/{quiz.Id}", quiz.Id);
});


app.MapGet("/user", async ([FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper) => {
    var users = await db.Users.ToListAsync();
    return mapper.Map<List<UserGetDTO>>(users);
});

app.MapGet("/user/rankings", async([FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper) =>
{
    int take = 10;
    var daily = db.Users.OrderBy(u => -u.DailyQuoins).Take(take).ToList();
    var weekly = db.Users.OrderBy(u => -u.WeeklyQuoins).Take(take).ToList();
    var monthly = db.Users.OrderBy(u => -u.MonthlyQuoins).Take(take).ToList();

    return Results.Ok(new UserRankingGetDTO()
    {
        Daily = mapper.Map<List<UserExtendedGetDTO>>(daily),
        Weekly = mapper.Map<List<UserExtendedGetDTO>>(weekly),
        Monthly = mapper.Map<List<UserExtendedGetDTO>>(monthly),
    });
}); 

app.MapPost("/user", async ([FromBody] UserPostDTO userDTO, [FromServices] IValidator<UserPostDTO> validator,
    [FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper) =>
{
    var validationResult = await validator.ValidateAsync(userDTO);
    if (!validationResult.IsValid)
    {
        return Results.ValidationProblem(validationResult.ToDictionary());
    }
    var user = mapper.Map<User>(userDTO);
    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Created($"/question/{user.Id}", user.Id);
});
app.MapPost("/session", async ([FromBody] UserPostDTO userDTO,
    [FromServices] QuizonomyDbContext db, HttpContext context, [FromServices] TokenService tokenService, AuthSettings auth, IMapper mapper) =>
{
    var user = await db.Users.Where(u => u.Username == userDTO.Username).FirstOrDefaultAsync();
    if(user is null)
    {
        return Results.NotFound("Nie odnaleziono u¿ytkownika");
    }
    if (!BCrypt.Net.BCrypt.Verify(userDTO.Password, user.Password))
    {
        return Results.NotFound("Dane logowania nie s¹ poprawne");
    }

    string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(256));
    var session = new Session() { Key = key, UserId = user.Id };
    db.Sessions.Add(session);
    await db.SaveChangesAsync();

    return Results.Ok(mapper.Map<SessionDTO>(session));
});
app.MapDelete("/session", [Authorize] async ([FromQuery] string key, [FromServices] QuizonomyDbContext db, HttpContext context) =>
{
    var identity = context.User.Identity as ClaimsIdentity;
    if (identity is null) return Results.Problem();
    var nameClaim = identity.FindFirst(ClaimTypes.Name);
    if (nameClaim is null) return Results.Problem();
    var user = db.Users.Where(u => u.Username == nameClaim.Value).FirstOrDefault();
    if (user is null) return Results.Problem();

    var session = await db.Sessions.Where(s => s.Key == key && s.UserId == user.Id).FirstOrDefaultAsync();
    if (session is null) return Results.NotFound();
    db.Sessions.Remove(session);
    return Results.Ok();
});
app.MapGet("/session", [Authorize] async ([FromServices] QuizonomyDbContext db, HttpContext context, IMapper mapper) =>
{
    var identity = context.User.Identity as ClaimsIdentity;
    if (identity is null) return Results.Problem();
    var nameClaim = identity.FindFirst(ClaimTypes.Name);
    if (nameClaim is null) return Results.Problem();
    var user = db.Users.Where(u => u.Username == nameClaim.Value).FirstOrDefault();
    if (user is null) return Results.Problem();

    return Results.Ok(mapper.Map<UserExtendedGetDTO>(user));
});

app.Run();