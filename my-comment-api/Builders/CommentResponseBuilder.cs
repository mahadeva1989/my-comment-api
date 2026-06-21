using my_comment_api.DTOs;

namespace my_comment_api.Builders;

public class CommentResponseBuilder
{
    private int _id;
    private string? _content;
    private string? _author;
    private DateTime _createdAt;

    public CommentResponseBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public CommentResponseBuilder WithContent(string? content)
    {
        _content = content;
        return this;
    }

    public CommentResponseBuilder WithAuthor(string? author)
    {
        _author = author;
        return this;
    }

    public CommentResponseBuilder WithCreateAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public CommentResponse Build()
    {
        if (_id <= 0)
            throw new InvalidOperationException("Comment Id must be greater than 0");

        if (_createdAt == default)
            throw new InvalidOperationException("CreatedAt must be set");

        return new CommentResponse
        {
            Id = _id,
            Content = _content,
            Author = _author,
            CreatedAt = _createdAt
        };
    }

}