using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CmsBillboard.DynamicContent.Models;

public class SurveyItem
{
    [Key]
    public string Id { get; set; } = string.Empty; // e.g. "samsung-zfold5"

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Question { get; set; } = string.Empty;

    [MaxLength(100)]
    public string BrandName { get; set; } = string.Empty;

    public string? AdHeadline { get; set; }

    public string? AdSubheadline { get; set; }

    public string? AdImageUrl { get; set; }

    public string? AdVideoUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SurveyOption> Options { get; set; } = new();
}

public class SurveyOption
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string SurveyId { get; set; } = string.Empty;

    [ForeignKey(nameof(SurveyId))]
    public SurveyItem? Survey { get; set; }

    [Required]
    [MaxLength(10)]
    public string OptionKey { get; set; } = string.Empty; // "A", "B", "C", "D"

    [Required]
    [MaxLength(200)]
    public string OptionText { get; set; } = string.Empty;

    [MaxLength(20)]
    public string ColorHex { get; set; } = "#3b82f6";

    public int VoteCount { get; set; } = 0;

    public int SortOrder { get; set; } = 0;
}

public class SurveyVoteLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string SurveyId { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string OptionKey { get; set; } = string.Empty;

    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string SiteId { get; set; } = "Default"; // e.g. "Surabaya", "Jakarta", "Medan"

    [Required]
    [MaxLength(150)]
    public string DeviceFingerprint { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? IpAddress { get; set; }

    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
}

// DTOs
public class SurveyVoteRequest
{
    [Required]
    public string SurveyId { get; set; } = string.Empty;

    [Required]
    public string OptionKey { get; set; } = string.Empty;

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
    public string Title { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string? AdHeadline { get; set; }
    public string? AdSubheadline { get; set; }
    public string? AdImageUrl { get; set; }
    public string SiteId { get; set; } = string.Empty;
    public int TotalVotes { get; set; }
    public List<SurveyOptionResultDto> Options { get; set; } = new();
}

public class SurveyOptionResultDto
{
    public string OptionKey { get; set; } = string.Empty;
    public string OptionText { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#3b82f6";
    public int VoteCount { get; set; }
    public double Percentage { get; set; }
}
