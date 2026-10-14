using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<SurveyItem> Surveys => Set<SurveyItem>();
    public DbSet<SurveyOption> SurveyOptions => Set<SurveyOption>();
    public DbSet<SurveyVoteLog> SurveyVoteLogs => Set<SurveyVoteLog>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Indexes for fast lookups & unique constraints
        modelBuilder.Entity<SurveyVoteLog>()
            .HasIndex(v => new { v.SurveyId, v.DeviceFingerprint });

        modelBuilder.Entity<SurveyVoteLog>()
            .HasIndex(v => new { v.SurveyId, v.SiteId });

        modelBuilder.Entity<SurveyOption>()
            .HasIndex(o => new { o.SurveyId, o.OptionKey });

        modelBuilder.Entity<AdminUser>()
            .HasIndex(u => u.Username)
            .IsUnique();

        // Seed default Admin user (admin / Admin123!)
        modelBuilder.Entity<AdminUser>().HasData(
            new AdminUser
            {
                Id = 1,
                Username = "admin",
                PasswordHash = "100000.0tCX+ZkJzcQ0v1TrYUcoow==.mb+4P1TYLOdMNoYS0huq0KkwHBchms+S6AbTeljUibQ=",
                FullName = "Super Administrator",
                Role = "SuperAdmin",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Seed default Samsung Galaxy Z Fold5 Survey as specified in the document
        var samsungSurvey = new SurveyItem
        {
            Id = "samsung-zfold5",
            Title = "SURVEY PENGGUNA SAMSUNG MOBILE",
            Question = "Manakah fitur Samsung yang PALING ANDA SUKAI?",
            BrandName = "Samsung",
            AdHeadline = "REVOLUSI LAYAR LIPAT",
            AdSubheadline = "SAMSUNG GALAXY Z FOLD5",
            AdImageUrl = "/images/samsung_galaxy_fold.png",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        modelBuilder.Entity<SurveyItem>().HasData(samsungSurvey);

        modelBuilder.Entity<SurveyOption>().HasData(
            new SurveyOption
            {
                Id = 1,
                SurveyId = "samsung-zfold5",
                OptionKey = "A",
                OptionText = "Baterai Tahan Lama",
                ColorHex = "#3B82F6", // Bright Blue
                VoteCount = 47,
                SortOrder = 1
            },
            new SurveyOption
            {
                Id = 2,
                SurveyId = "samsung-zfold5",
                OptionKey = "B",
                OptionText = "Kamera Tajam",
                ColorHex = "#10B981", // Emerald Green
                VoteCount = 33,
                SortOrder = 2
            },
            new SurveyOption
            {
                Id = 3,
                SurveyId = "samsung-zfold5",
                OptionKey = "C",
                OptionText = "Anti Air IPX8",
                ColorHex = "#F59E0B", // Amber Gold
                VoteCount = 10,
                SortOrder = 3
            },
            new SurveyOption
            {
                Id = 4,
                SurveyId = "samsung-zfold5",
                OptionKey = "D",
                OptionText = "AI Image Editing",
                ColorHex = "#8B5CF6", // Purple
                VoteCount = 10,
                SortOrder = 4
            }
        );
    }
}
