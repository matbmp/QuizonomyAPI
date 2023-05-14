using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using QuizonomyAPI.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace QuizonomyAPI.Services
{
    public class TokenService
    {
        private readonly TokenValidationParameters _validationParameters;
        private readonly QuizonomyDbContext _db;
        private readonly JwtSecurityTokenHandler _tokenHandler = new JwtSecurityTokenHandler();
        private readonly AuthSettings _jwtSettings;

        public TokenService([FromServices] QuizonomyDbContext db, AuthSettings jwtSettings)
        {
            _db = db;
            _jwtSettings = jwtSettings;
            _validationParameters = new TokenValidationParameters
            {
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                ValidateIssuerSigningKey = true,
            };
        }

        public Task<TokenValidationResult> ValidateTokenAsync(string token)
        {
            return _tokenHandler.ValidateTokenAsync(token, _validationParameters);
        }

        public async Task<string> GenerateRefreshTokenForAsync(User user)
        {
            string key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(256));
            _db.Sessions.Add(new Session() { Key = key, UserId = user.Id });
            await _db.SaveChangesAsync();
            return key;
        }

        public async Task InvalidateTokenAsync(string token)
        {
            _db.RemoveRange(_db.Sessions.Where(s => s.Key == token));
            await _db.SaveChangesAsync();
        }

        public async Task<string?> GenerateNewAccessTokenAsync(string refreshToken)
        {
            var user = await _db.Sessions.Where(s => s.Key == refreshToken).Select(s => s.User).FirstOrDefaultAsync();
            if (user is not null)
            {
                return GenerateAccessTokenFor(user);
            }
            return null;
        }

        public string GenerateAccessTokenFor(User user)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            SigningCredentials credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
            };

            var token = new JwtSecurityToken(_jwtSettings.Issuer,
                _jwtSettings.Audience,
                claims,
                expires: DateTime.Now.AddHours(1),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
