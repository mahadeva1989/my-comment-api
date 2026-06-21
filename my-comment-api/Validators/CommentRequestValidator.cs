using FluentValidation;

public class CommentRequestValidator : AbstractValidator<CommentRequest>
{
    public CommentRequestValidator()
    {
        RuleFor(x => x.Content)
        .NotEmpty().WithMessage("Comment cannot be empty");
    }

}