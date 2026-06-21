using Microsoft.EntityFrameworkCore;
using my_comment_api.Models;

namespace my_comment_api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Comment> Comments => Set<Comment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.ReferenceId).IsRequired();
            entity.Property(u => u.Username).IsRequired()
            .HasMaxLength(50);
            entity.Property(u => u.Email).HasMaxLength(50)
            .IsRequired();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Content).IsRequired().HasMaxLength(100);
            entity.Property(c => c.Author).HasMaxLength(50);
            entity.HasOne(c => c.User)
            .WithMany(c => c.Comments)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        });
    }

}