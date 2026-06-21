namespace my_comment_api.Models;

public class Comment
{
    public int Id { get; set; }
    public string? Content { get; set; }
    public string? Author { get; set; }
    public DateTime CreatedAt { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
}