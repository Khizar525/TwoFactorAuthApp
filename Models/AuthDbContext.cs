using Microsoft.EntityFrameworkCore;

namespace TwoFactorAuthApp.Models;

public class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    public DbSet<UserModel> Users { get; set; }
}
