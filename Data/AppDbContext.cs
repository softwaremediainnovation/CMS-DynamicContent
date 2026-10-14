using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<SurveyMaster> SurveyMasters => Set<SurveyMaster>();
    public DbSet<SurveyQuestion> SurveyQuestions => Set<SurveyQuestion>();
    public DbSet<SurveyOption> SurveyOptions => Set<SurveyOption>();
    public DbSet<SurveyVoteLog> SurveyVoteLogs => Set<SurveyVoteLog>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Indexes for performance & anti-spam constraints
        modelBuilder.Entity<SurveyVoteLog>()
            .HasIndex(v => new { v.SurveyMasterId, v.DeviceFingerprint });

        modelBuilder.Entity<SurveyVoteLog>()
            .HasIndex(v => new { v.QuestionId, v.OptionKey });

        modelBuilder.Entity<SurveyVoteLog>()
            .HasIndex(v => new { v.SurveyMasterId, v.SiteId });

        modelBuilder.Entity<SurveyVoteLog>()
            .HasIndex(v => new { v.SurveyMasterId, v.City });

        modelBuilder.Entity<SurveyOption>()
            .HasIndex(o => new { o.QuestionId, o.OptionKey });

        modelBuilder.Entity<SurveyQuestion>()
            .HasIndex(q => q.SurveyMasterId);

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

        // Seed default Samsung Galaxy Z Fold5 Master Survey (Modul 7)
        modelBuilder.Entity<SurveyMaster>().HasData(
            new SurveyMaster
            {
                Id = "samsung-zfold5",
                ClientName = "Samsung",
                CampaignTitle = "SURVEY PENGGUNA SAMSUNG MOBILE",
                AdHeadline = "REVOLUSI LAYAR LIPAT",
                AdSubheadline = "SAMSUNG GALAXY Z FOLD5",
                BannerImageUrl = "/images/samsung_galaxy_fold.svg",
                BrandLogoUrl = null,
                ThemeColorHex = "#0072FF",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Seed default Samsung Survey Question
        modelBuilder.Entity<SurveyQuestion>().HasData(
            new SurveyQuestion
            {
                Id = 1,
                SurveyMasterId = "samsung-zfold5",
                QuestionText = "Manakah fitur Samsung yang PALING ANDA SUKAI?",
                QuestionType = "MultipleChoice",
                QuestionImageUrl = null,
                SortOrder = 1
            }
        );

        // Seed default Samsung Options for Question 1
        modelBuilder.Entity<SurveyOption>().HasData(
            new SurveyOption
            {
                Id = 1,
                QuestionId = 1,
                OptionKey = "A",
                OptionText = "Baterai Tahan Lama",
                OptionImageUrl = null,
                ColorHex = "#3B82F6", // Bright Blue
                VoteCount = 47,
                SortOrder = 1
            },
            new SurveyOption
            {
                Id = 2,
                QuestionId = 1,
                OptionKey = "B",
                OptionText = "Kamera Tajam",
                OptionImageUrl = null,
                ColorHex = "#10B981", // Emerald Green
                VoteCount = 33,
                SortOrder = 2
            },
            new SurveyOption
            {
                Id = 3,
                QuestionId = 1,
                OptionKey = "C",
                OptionText = "Anti Air IPX8",
                OptionImageUrl = null,
                ColorHex = "#F59E0B", // Amber Gold
                VoteCount = 10,
                SortOrder = 3
            },
            new SurveyOption
            {
                Id = 4,
                QuestionId = 1,
                OptionKey = "D",
                OptionText = "AI Image Editing",
                OptionImageUrl = null,
                ColorHex = "#8B5CF6", // Purple
                VoteCount = 10,
                SortOrder = 4
            }
        );
    }
}
