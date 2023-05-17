using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using QuizonomyAPI.Models;
using QuizonomyAPI.Services;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace QuizonomyAPI.Middleware
{
    public class CustomAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly TokenService _tokenService;
        private readonly QuizonomyDbContext _context;
        public CustomAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            TokenService tokenService,
            QuizonomyDbContext context,
            AuthSettings authSettings) : base(options, logger, encoder, clock)
        {
            _tokenService = tokenService;
            _context = context;
        }
        protected async override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var authorizationHeaders = Request.Headers.Authorization;
            var sessionKey = authorizationHeaders.FirstOrDefault();
            var session = await _context.Sessions.Where(s => s.Key == sessionKey).Include(s => s.User).FirstOrDefaultAsync();

            if (session is not null)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, session.User.Username),
                };
                
                AuthenticationTicket ticket = new AuthenticationTicket(
                        new ClaimsPrincipal(new ClaimsIdentity(claims, "SESSION")),
                         this.Scheme.Name
                        );
                return AuthenticateResult.Success(ticket);
            }
            return AuthenticateResult.NoResult();
        }
    }
}
