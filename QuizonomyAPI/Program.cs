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


app.MapGet("/quiz/{id}", async (long id, QuizonomyDbContext db, IMapper mapper) =>
{
    var quiz = await db.Quizzes.ById(id).WithAuthorAndQuestions().FirstOrDefaultAsync();
    return quiz switch
    {
        null => Results.NotFound(),
        Quiz => Results.Ok(mapper.Map<QuizGetDTO>(quiz))
    };
});
app.MapGet("/quiz", async ([FromQuery] string searchQuery, [FromQuery] int skip, [FromQuery] int take,
    [FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper) =>
{
    // Je¿eli zapytanie zawiera tekst wyszukiwania to kolejnoœæ jest na podstawie trygramów
    // Je¿eli zapytanie nie zawiera tekstu wyszukiwania to kolejnoœæ jest na podstawie iloœci wykonañ danego quizu
    var dbQuery = searchQuery switch
    {
        string => db.Quizzes.OrderBy(q => -EF.Functions.TrigramsSimilarity(q.Name, searchQuery)),
        null => db.Quizzes.OrderBy(q => -q.AttemptCount)
    };
    var quizzes = await dbQuery.SkipTake(skip, take).WithAuthorAndQuestions().ToListAsync();
    return mapper.Map<ICollection<QuizGetDTO>>(quizzes);
});
app.MapGet("/quiz/popular", async ([FromQuery] int take, [FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper) =>
{
    // kolejnoœæ na podstawie iloœci wykonanñ quizu
    var quizzes = await db.Quizzes.OrderBy(q => -q.AttemptCount).Take(take).WithAuthorAndQuestions().ToListAsync();
    return mapper.Map<ICollection<QuizGetDTO>>(quizzes);
});
app.MapGet("/quiz/daily", [Authorize] async ([FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper, HttpContext context) =>
{
    // Zapytanie przesz³o przez middleware autoryzacji ale nie mo¿na odczytaæ u¿ytkownika - b³¹d serwera
    if (await getLoggedUserAsync(db, context) is not User user) return Results.Problem();
    // U¿tkownik wyczerpa³ dzisiejsze szanse
    if (user.DailyCount <= 0) return Results.BadRequest();
    // Pobieraj¹æ losowy quiz, nie znaleziono ¿adnego w bazie danych
    if (await db.Quizzes.OrderBy(q => Guid.NewGuid()).FirstOrDefaultAsync() is not Quiz quiz) return Results.NotFound();
    
    // Po obs³u¿eniu przypdaków negatynych dokonujemy przypisania quizu do u¿ytkownika
    user.DailyQuizId = quiz.Id;
    user.DailyCount--;
    await db.SaveChangesAsync();
    return Results.Ok(quiz.Id);
});
app.MapPost("/quiz/submit", [Authorize] async ([FromBody] QuizAttemptPostDTO attemptDTO, [FromServices] QuizonomyDbContext db, HttpContext context) =>
{
    // Zapytanie przesz³o przez middleware autoryzacji ale nie mo¿na odczytaæ u¿ytkownika - b³¹d serwera
    if (await getLoggedUserAsync(db, context) is not User user) return Results.Problem();

    int quoins = 0;

    // U¿ytkownik wykona³ przypisany mu quiz
    if (user.DailyQuizId == attemptDTO.QuizId)
    {
        var quiz = await db.Quizzes.ById(attemptDTO.QuizId).WithAuthorAndQuestions().FirstOrDefaultAsync();
        if (quiz is null) return Results.NotFound();

        // Uniewa¿niamy przypisanie quizu
        user.DailyQuizId = -1;

        // Wyliczamy zdobyte punkty i przypisujemy je u¿ytkownikowi
        float correctCount = attemptDTO.CorrectCount;
        int questionCount = quiz.Questions.Count();
        quoins = (int)((correctCount / questionCount) * (100 * MathF.Sqrt(questionCount)));
        user.DailyQuoins += quoins;
        user.WeeklyQuoins += quoins;
        user.MonthlyQuoins += quoins;
    }
    var attempt = await db.QuizAttempts.ByQuizIdAndUserId(attemptDTO.QuizId, user.Id).FirstOrDefaultAsync();

    // je¿eli próba wype³nienia quizu nie istnieje w bazie to j¹ dodajemy
    // w przeciwnym razie modyfikujemy wartoœæ w bazie je¿eli nowy wynik jest lepszy (wy¿szy)
    if (attempt is not null)
    {
        float oldRatio = 1000 * attempt.CorrectCount / (float)(attempt.TimeMilliseconds);
        float newRatio = 1000 * attemptDTO.CorrectCount / (float)(attemptDTO.TimeMilliseconds);
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

    // Modyfikujemy najlepsze podejœcie je¿eli rozwa¿ane podejœcie rzeczywiœcie jest najlepsze
    float attemptScore = 1000 * attemptDTO.CorrectCount / (float)(attemptDTO.TimeMilliseconds);
    var quiz2 = db.Quizzes.ById(attemptDTO.QuizId).FirstOrDefault();
    if (quiz2 is not null)
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
    QuizSubmitResultDTO result = new QuizSubmitResultDTO() { Points = quoins };
    return Results.Ok(result);
});
app.MapPost("/quiz", async ([FromBody] QuizPostDTO quizDTO, [FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper) =>
{
    var quiz = mapper.Map<Quiz>(quizDTO);
    db.Quizzes.Add(quiz);
    await db.SaveChangesAsync();

    return Results.Created($"/question/{quiz.Id}", quiz.Id);
});





app.MapGet("/user", async ([FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper) =>
{
    var users = await db.Users.ToListAsync();
    return mapper.Map<List<UserGetDTO>>(users);
});

app.MapGet("/user/rankings", ([FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper) =>
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

    return Results.Created($"/user/{user.Id}", user.Id);
});





app.MapPost("/session", async ([FromBody] UserPostDTO userDTO,
    [FromServices] QuizonomyDbContext db, HttpContext context, [FromServices] TokenService tokenService, AuthSettings auth, IMapper mapper) =>
{
    var user = await db.Users.ByUsername(userDTO.Username).FirstOrDefaultAsync();
    if (user is null)
    {
        return Results.NotFound("Nie odnaleziono u¿ytkownika");
    }
    if (!BCrypt.Net.BCrypt.Verify(userDTO.Password, user.Password))
    {
        return Results.NotFound("Dane logowania nie s¹ poprawne");
    }

    // Je¿eli nazwa u¿ytkownika oraz has³o s¹ poprawne to towrzymy now¹ sesjê
    string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(256));
    var session = new Session() { Key = key, UserId = user.Id };
    db.Sessions.Add(session);
    await db.SaveChangesAsync();

    return Results.Ok(mapper.Map<SessionDTO>(session));
});
app.MapDelete("/session", [Authorize] async ([FromQuery] string key, [FromServices] QuizonomyDbContext db, HttpContext context) =>
{
    // Zapytanie przesz³o przez middleware autoryzacji ale nie mo¿na odczytaæ u¿ytkownika - b³¹d serwera
    if (await getLoggedUserAsync(db, context) is not User user) return Results.Problem();

    // Usuwamy sesjê
    var session = await db.Sessions.ByKeyIdAndUserId(key, user.Id).FirstOrDefaultAsync();
    if (session is null) return Results.NotFound();
    db.Sessions.Remove(session);
    await db.SaveChangesAsync();
    return Results.Ok();
});
app.MapGet("/session", [Authorize] async ([FromServices] QuizonomyDbContext db, HttpContext context, IMapper mapper) =>
{
    // Zapytanie przesz³o przez middleware autoryzacji ale nie mo¿na odczytaæ u¿ytkownika - b³¹d serwera
    if (await getLoggedUserAsync(db, context) is not User user) return Results.Problem();

    // Je¿eli ostanie dzienne próby zosta³y przypisane dnia wczorajszego, to przypisujemy nowe próby
    // i ustawiamy dzisiejsz¹ datê przypisania dziennych prób
    if (user.DailyQuizDate.Date != DateTimeOffset.Now.Date)
    {
        user.DailyCount = 3;
        user.DailyQuizDate = DateTimeOffset.Now.Date;
        db.SaveChanges();
    }

    return Results.Ok(mapper.Map<UserExtendedGetDTO>(user));
});





async Task<User?> getLoggedUserAsync(QuizonomyDbContext db, HttpContext context)
{
    if (context.User.Identity is not ClaimsIdentity identity) return null;
    if (identity.FindFirst(ClaimTypes.Name) is not Claim nameClaim) return null;
    if (await db.Users.ByUsername(nameClaim.Value).FirstOrDefaultAsync() is not User user) return null;
    return user;
}

app.Run();