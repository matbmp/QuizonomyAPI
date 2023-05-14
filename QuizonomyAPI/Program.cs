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

var builder = WebApplication.CreateBuilder(args);

AuthSettings authSettings = new AuthSettings();
builder.Configuration.GetSection("Auth").Bind(authSettings);
authSettings.CookieOptions = new CookieOptions()
{
    HttpOnly = true,
    SameSite = SameSiteMode.None,
    Secure = false,
    //Secure = true,
    IsEssential = true,
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
    app.UseCors();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.UseHttpsRedirection();


app.MapGet("/quiz/{id}", async (long id, QuizonomyDbContext db, IMapper mapper) => {
    var quiz = await db.Quizzes.Where(q => q.Id == id).Include(q => q.Author).Include(q => q.Questions).FirstOrDefaultAsync();
    if (quiz is null) return Results.NotFound();
    return Results.Ok(mapper.Map<QuizGetDTO>(quiz));
    });
app.MapGet("/quiz", async ([FromQuery] string searchQuery, [FromQuery] int skip, [FromQuery] int take, [FromServices]QuizonomyDbContext db, [FromServices]IMapper mapper) =>
{
    var quizzes = await db.Quizzes.OrderBy(q => -EF.Functions.TrigramsSimilarity(q.Name, searchQuery))
        .Skip(skip).Take(take).Include(q => q.Author).Include(q => q.Questions).ToListAsync();
    return mapper.Map<ICollection<QuizGetDTO>>(quizzes);
});
app.MapGet("/quiz/random", async ([FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper) =>
{
    var quizzes = await db.Quizzes.OrderBy(q => Guid.NewGuid()).Take(1).Include(q => q.Author).Include(q => q.Questions).ToListAsync();
    return mapper.Map<ICollection<QuizGetDTO>>(quizzes);
});
app.MapGet("/quiz/daily", [Authorize] async ([FromServices] QuizonomyDbContext db, [FromServices] IMapper mapper, HttpContext context) =>
{
    var identity = context.User.Identity as ClaimsIdentity;
    if(identity is null) return Results.Problem();
    var nameClaim = identity.FindFirst(ClaimTypes.Name);
    if (nameClaim is null) return Results.Problem();
    var user = db.Users.Where(u => u.Username== nameClaim.Value).FirstOrDefault();
    if(user is null) return Results.Problem();

    if(user.DailyQuizDate.Date != DateTime.Now.Date)
    {
        user.DailyCount = 3;
        user.DailyQuizDate = DateTime.Now.Date;
        await db.SaveChangesAsync();
    }
    var quiz = await db.Quizzes.OrderBy(q => Guid.NewGuid()).FirstOrDefaultAsync();
    if(quiz is null) return Results.NotFound();
    user.DailyQuizId = quiz.Id;
    user.DailyCount--;
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
        float oldRatio = attempt.CorrectCount / (float)(attempt.Time.Ticks / TimeSpan.TicksPerMillisecond);
        float newRatio = attemptDTO.CorrectCount / (float)(attemptDTO.Time.Ticks / TimeSpan.TicksPerMillisecond);
        if (newRatio > oldRatio)
        {
            attempt.Time = attemptDTO.Time;
            attempt.CorrectCount = attemptDTO.CorrectCount;
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
    [FromServices] QuizonomyDbContext db, HttpContext context, [FromServices] TokenService tokenService, AuthSettings auth) =>
{
    var user = await db.Users.Where(u => u.Username == userDTO.Username).FirstOrDefaultAsync();
    if(user is null)
    {
        return Results.NotFound();
    }
    if (!BCrypt.Net.BCrypt.Verify(userDTO.Password, user.Password))
    {
        return Results.Unauthorized();
    }

    var access = tokenService.GenerateAccessTokenFor(user);
    context.Response.Cookies.Append(auth.AccessCookie, access, authSettings.CookieOptions);
    var refresh = await tokenService.GenerateRefreshTokenForAsync(user);
    context.Response.Cookies.Append(auth.RefreshCookie, refresh, authSettings.CookieOptions);
    return Results.Ok();
});
app.MapDelete("/session", [Authorize] async ([FromServices] QuizonomyDbContext db, HttpContext context,
    [FromServices] TokenService tokenService, AuthSettings auth) =>
{
    var refresh = context.Request.Cookies[auth.RefreshCookie];
    if(refresh is not null)
    {
        await tokenService.InvalidateTokenAsync(refresh);
        context.Response.Cookies.Delete(auth.RefreshCookie);
        return Results.Ok("Session invalidated");
    }
    context.Response.Cookies.Delete(auth.AccessCookie);
    return Results.Ok("No session present");
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