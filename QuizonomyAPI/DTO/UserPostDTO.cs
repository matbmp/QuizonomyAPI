using FluentValidation;

namespace QuizonomyAPI.DTO
{
    public class UserPostDTO
    {
        public string Username { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class UserPostDTOValidator : AbstractValidator<UserPostDTO>
    {
        public UserPostDTOValidator() {
            RuleFor(x => x.Username).NotEmpty().WithMessage("Your username cannot be empty")
                .MinimumLength(3).WithMessage("Your username must be at least 3 characters long.")
                .MaximumLength(24).WithMessage("Your username cannot be longer than 24 characters.");
            RuleFor(p => p.Password).NotEmpty().WithMessage("Your password cannot be empty")
                    .MinimumLength(8).WithMessage("Your password length must be at least 8.")
                    .MaximumLength(24).WithMessage("Your password length must not exceed 24.")
                    .Matches(@"[A-Z]+").WithMessage("Your password must contain at least one uppercase letter.")
                    .Matches(@"[a-z]+").WithMessage("Your password must contain at least one lowercase letter.")
                    .Matches(@"[0-9]+").WithMessage("Your password must contain at least one number.")
                    .Matches(@"[\!\?\*\.]+").WithMessage("Your password must contain at least one (!? *.).");
        }
    }
}
