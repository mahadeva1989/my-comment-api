using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace my_comment_api.Models;

public class User
{
    public int Id { get; set; }
    public Guid ReferenceId { get; set; } = Guid.NewGuid();
    public string? Username { get; set; }
    public string? Email { get; set; }
    public string? PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<Comment> Comments { get; set; } = new List<Comment>();
    public string Role { get; set; } = "Author";
}