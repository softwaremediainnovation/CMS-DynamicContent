using Microsoft.EntityFrameworkCore;
using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<DynamicContentMaster> DynamicContentMasters => Set<DynamicContentMaster>();
    public DbSet<DynamicContentQuestion> DynamicContentQuestions => Set<DynamicContentQuestion>();
    public DbSet<DynamicContentOption> DynamicContentOptions => Set<DynamicContentOption>();
    public DbSet<DynamicContentVoteLog> DynamicContentVoteLogs => Set<DynamicContentVoteLog>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Indexes for performance & anti-spam constraints
        modelBuilder.Entity<DynamicContentVoteLog>()
            .HasIndex(v => new { v.DynamicContentMasterId, v.DeviceFingerprint });

        modelBuilder.Entity<DynamicContentVoteLog>()
            .HasIndex(v => new { v.QuestionId, v.OptionKey });

        modelBuilder.Entity<DynamicContentVoteLog>()
            .HasIndex(v => new { v.DynamicContentMasterId, v.SiteId });

        modelBuilder.Entity<DynamicContentVoteLog>()
            .HasIndex(v => new { v.DynamicContentMasterId, v.City });

        modelBuilder.Entity<DynamicContentOption>()
            .HasIndex(o => new { o.QuestionId, o.OptionKey });

        modelBuilder.Entity<DynamicContentQuestion>()
            .HasIndex(q => q.DynamicContentMasterId);

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

        // Seed default Samsung Galaxy Z Fold5 Master Content
        modelBuilder.Entity<DynamicContentMaster>().HasData(
            new DynamicContentMaster
            {
                Id = "samsung-zfold5",
                ClientName = "Samsung",
                CampaignTitle = "SURVEY PENGGUNA SAMSUNG MOBILE",
                AdHeadline = "REVOLUSI LAYAR LIPAT",
                AdSubheadline = "SAMSUNG GALAXY Z FOLD5",
                BannerImageUrl = "/images/samsung_galaxy_fold.svg",
                BrandLogoUrl = null,
                ThemeColorHex = "#0072FF",
                ContentType = "Survey",
                ParticipationMode = "onetime",
                GapDurationMinutes = 10,
                LocationTagDetect = false,
                WelcomeMessage = "Selamat Datang di Interaktif DOOH Warna Warni",
                InstructionComment = "Silakan isi nama dan nomor telepon untuk berpartisipasi",
                IsActive = true,
                CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        // Seed default Samsung Question
        modelBuilder.Entity<DynamicContentQuestion>().HasData(
            new DynamicContentQuestion
            {
                Id = 1,
                DynamicContentMasterId = "samsung-zfold5",
                QuestionText = "Manakah fitur Samsung yang PALING ANDA SUKAI?",
                QuestionType = "multiple",
                QuestionImageUrl = null,
                CorrectOptionKey = null,
                SortOrder = 1
            }
        );

        // Seed default Samsung Options for Question 1
        modelBuilder.Entity<DynamicContentOption>().HasData(
            new DynamicContentOption
            {
                Id = 1,
                QuestionId = 1,
                OptionKey = "A",
                OptionText = "Baterai Tahan Lama",
                OptionImageUrl = null,
                ColorHex = "#3B82F6", // Bright Blue
                IsCorrect = false,
                VoteCount = 47,
                SortOrder = 1
            },
            new DynamicContentOption
            {
                Id = 2,
                QuestionId = 1,
                OptionKey = "B",
                OptionText = "Kamera Tajam",
                OptionImageUrl = null,
                ColorHex = "#10B981", // Emerald Green
                IsCorrect = false,
                VoteCount = 33,
                SortOrder = 2
            },
            new DynamicContentOption
            {
                Id = 3,
                QuestionId = 1,
                OptionKey = "C",
                OptionText = "Anti Air IPX8",
                OptionImageUrl = null,
                ColorHex = "#F59E0B", // Amber Gold
                IsCorrect = false,
                VoteCount = 10,
                SortOrder = 3
            },
            new DynamicContentOption
            {
                Id = 4,
                QuestionId = 1,
                OptionKey = "D",
                OptionText = "AI Image Editing",
                OptionImageUrl = null,
                ColorHex = "#8B5CF6", // Purple
                IsCorrect = false,
                VoteCount = 10,
                SortOrder = 4
            }
        );
    }
}
