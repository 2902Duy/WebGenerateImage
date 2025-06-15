using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebGenerateImage.Models;

public class AppDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
{
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<UserFaceImage> UserFaceImages => Set<UserFaceImage>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
