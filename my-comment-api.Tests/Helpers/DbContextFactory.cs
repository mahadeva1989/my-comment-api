using Microsoft.EntityFrameworkCore;
using my_comment_api.Data;

public static class DbContextFactory
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString())
        .Options;

        return new AppDbContext(options);
    }
}