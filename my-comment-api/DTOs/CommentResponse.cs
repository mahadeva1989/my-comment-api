namespace my_comment_api.DTOs;

public class CommentResponse
{
    public int Id { get; set; }
    public string? Content { get; set; }
    public string? Author { get; set; }
    public DateTime CreatedAt { get; set; }
}