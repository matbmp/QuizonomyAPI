using System.ComponentModel.DataAnnotations;

namespace QuizonomyAPI
{
    public class AuthSettings
    {
        [Required]
        public string Issuer { get; set; }
        [Required]
        public string Audience { get; set; }
        [Required]
        [MinLength(128)]
        public string Key { get; set; }

        public string AuthenticationScheme { get; set; }
        public string AccessCookie { get; set; }
        public string RefreshCookie { get; set; }

        public CookieOptions CookieOptions { get; set; }
    }
}
