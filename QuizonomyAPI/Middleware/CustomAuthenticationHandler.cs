using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuizonomyAPI.Services;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace QuizonomyAPI.Middleware
{
    public class CustomAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly TokenService _tokenService;
        private readonly AuthSettings _authSettings;
        public CustomAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock,
            TokenService tokenService,
            AuthSettings authSettings) : base(options, logger, encoder, clock)
        {
            _tokenService = tokenService;
            _authSettings = authSettings;
        }
        protected async override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? accessToken = Request.Cookies[_authSettings.AccessCookie];
            if (accessToken is not null)
            {
                TokenValidationResult validationResult = await _tokenService.ValidateTokenAsync(accessToken);
                if (validationResult.IsValid)
                {
                    AuthenticationTicket ticket = new AuthenticationTicket(
                        new ClaimsPrincipal(validationResult.ClaimsIdentity),
                        _authSettings.AuthenticationScheme
                        );
                    return AuthenticateResult.Success(ticket);
                }
            }

            string? refreshToken = Request.Cookies[_authSettings.RefreshCookie];
            if (refreshToken is not null)
            {
                string? newAccessToken = await _tokenService.GenerateNewAccessTokenAsync(refreshToken);
                if (newAccessToken is not null)
                {
                    Response.Cookies
                        .Append(_authSettings.AccessCookie, newAccessToken, _authSettings.CookieOptions);
                    TokenValidationResult validationResult = await _tokenService.ValidateTokenAsync(newAccessToken);
                    AuthenticationTicket ticket = new AuthenticationTicket(
                        new ClaimsPrincipal(validationResult.ClaimsIdentity),
                         _authSettings.AuthenticationScheme
                        );
                    return AuthenticateResult.Success(ticket);
                }
            }
            return AuthenticateResult.NoResult();
        }
    }
}
