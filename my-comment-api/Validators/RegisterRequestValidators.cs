using System.Data;
using FluentValidation;

namespace my_comment_api.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
        .NotEmpty().WithMessage("Username is required")
        .MinimumLength(3).WithMessage("Username must be atleast 3 characters")
        .MaximumLength(50).WithMessage("Username cannot contain more than 50 chracters");

        RuleFor(x => x.Email)
        .NotEmpty().WithMessage("Email is required")
        .MaximumLength(100).WithMessage("Email cannot exceed 100 characters")
        .EmailAddress().WithMessage("A valida email address is required");

        RuleFor(x => x.Password)
        .NotEmpty().WithMessage("Password is required")
        .MinimumLength(6).WithMessage("Password must be at least 6 characters")
        .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
        .Matches("[0-9]").WithMessage("Password must contain at least one number");

    }

}