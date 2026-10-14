using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CmsBillboard.DynamicContent.Models;

public class SurveyMaster
{
    [Key]
    [MaxLength(100)]
    public string Id { get; set; } = string.Empty; // e.g. "samsung-zfold5"

    [Required]
    [MaxLength(150)]
    public string ClientName { get; set; } = string.Empty; // e.g. "Samsung"

    [Required]
    [MaxLength(200)]
    public string CampaignTitle { get; set; } = string.Empty; // e.g. "SURVEY PENGGUNA SAMSUNG MOBILE"

    [MaxLength(200)]
    public string? AdHeadline { get; set; } // e.g. "REVOLUSI LAYAR LIPAT"

    [MaxLength(200)]
    public string? AdSubheadline { get; set; } // e.g. "SAMSUNG GALAXY Z FOLD5"

    public string? BannerImageUrl { get; set; } // URL/Path gambar poster iklan

    public string? BrandLogoUrl { get; set; } // URL/Path icon/logo klien

    [MaxLength(20)]
    public string ThemeColorHex { get; set; } = "#0072FF"; // Warna tema brand klien

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SurveyQuestion> Questions { get; set; } = new();
}

public class SurveyQuestion
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string SurveyMasterId { get; set; } = string.Empty;

    [ForeignKey(nameof(SurveyMasterId))]
    public SurveyMaster? SurveyMaster { get; set; }

    [Required]
    [MaxLength(500)]
    public string QuestionText { get; set; } = string.Empty; // Isi pertanyaan

    [Required]
    [MaxLength(50)]
    public string QuestionType { get; set; } = "MultipleChoice"; // "MultipleChoice", "Essay", "Rating"

    public string? QuestionImageUrl { get; set; } // Gambar khusus pendukung pertanyaan (opsional)

    public int SortOrder { get; set; } = 1;

    public List<SurveyOption> Options { get; set; } = new();

    public List<SurveyVoteLog> VoteLogs { get; set; } = new();
}

public class SurveyOption
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int QuestionId { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public SurveyQuestion? Question { get; set; }

    [Required]
    [MaxLength(10)]
    public string OptionKey { get; set; } = string.Empty; // "A", "B", "C", "D"

    [Required]
    [MaxLength(200)]
    public string OptionText { get; set; } = string.Empty; // Teks jawaban

    public string? OptionImageUrl { get; set; } // Icon/gambar opsi jawaban (opsional)

    [MaxLength(20)]
    public string ColorHex { get; set; } = "#3B82F6";

    public int VoteCount { get; set; } = 0;

    public int SortOrder { get; set; } = 1;
}

public class SurveyVoteLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int QuestionId { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public SurveyQuestion? Question { get; set; }

    [Required]
    [MaxLength(100)]
    public string SurveyMasterId { get; set; } = string.Empty;

    [MaxLength(10)]
    public string OptionKey { get; set; } = string.Empty; // "A", "B", "C", "D"

    [MaxLength(1000)]
    public string? TextResponse { get; set; } // Untuk jawaban teks bebas / essay

    [MaxLength(500)]
    public string? PhotoUrl { get; set; } // Untuk jawaban upload foto

    [MaxLength(100)]
    public string UserName { get; set; } = "Anonim";

    [MaxLength(50)]
    public string SiteId { get; set; } = "Default"; // Lokasi Videotron (Surabaya, Jakarta, dll)

    [Required]
    [MaxLength(150)]
    public string DeviceFingerprint { get; set; } = string.Empty; // Anti-spam 1x vote

    [MaxLength(60)]
    public string? IpAddress { get; set; }

    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
}

// DTOs
public class SurveyVoteRequest
{
    public string? Token { get; set; }

    public string SurveyId { get; set; } = string.Empty;

    public int QuestionId { get; set; }

    [Required]
    public string OptionKey { get; set; } = string.Empty;

    public string? TextResponse { get; set; }

    [Required]
    public string UserName { get; set; } = string.Empty;

    public string SiteId { get; set; } = "Default";

    [Required]
    public string DeviceFingerprint { get; set; } = string.Empty;
}

public class SurveyVoteResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SurveyResultDto? CurrentResults { get; set; }
}

public class SurveyResultDto
{
    public string SurveyId { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string CampaignTitle { get; set; } = string.Empty;
    public string? AdHeadline { get; set; }
    public string? AdSubheadline { get; set; }
    public string? BannerImageUrl { get; set; }
    public string? BrandLogoUrl { get; set; }
    public string ThemeColorHex { get; set; } = "#0072FF";
    public string SiteId { get; set; } = string.Empty;
    public int TotalVotes { get; set; }

    // Primary question (Modul 7)
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "MultipleChoice";
    public string? QuestionImageUrl { get; set; }

    public List<SurveyOptionResultDto> Options { get; set; } = new();
}

public class SurveyOptionResultDto
{
    public int OptionId { get; set; }
    public string OptionKey { get; set; } = string.Empty;
    public string OptionText { get; set; } = string.Empty;
    public string? OptionImageUrl { get; set; }
    public string ColorHex { get; set; } = "#3B82F6";
    public int VoteCount { get; set; }
    public double Percentage { get; set; }
}
