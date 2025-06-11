using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebGenerateImage.Models;

public class AppDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
{
    public DbSet<ImagePrompt> ImagePrompts { get; set; }
    public DbSet<ImageToImage> ImageToImages { get; set; }
    public DbSet<OtpCode> OtpCodes => Set<OtpCode>();
    public DbSet<FaceAuthentication> FaceAuthentications => Set<FaceAuthentication>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
}
