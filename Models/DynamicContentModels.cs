global using SurveyVoteRequest = CmsBillboard.DynamicContent.Models.DynamicContentVoteRequest;
global using SurveyVoteResponse = CmsBillboard.DynamicContent.Models.DynamicContentVoteResponse;
global using SurveyResultDto = CmsBillboard.DynamicContent.Models.DynamicContentResultDto;
global using SurveyOptionResultDto = CmsBillboard.DynamicContent.Models.DynamicContentOptionResultDto;

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CmsBillboard.DynamicContent.Models;

public class DynamicContentMaster
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

    // Parameter Tipe Konten: "Survey" (Jajak Pendapat) | "BrandIQ" (Quiz) | "Game" (Interactive Game)
    [MaxLength(50)]
    public string ContentType { get; set; } = "Survey";

    [MaxLength(150)]
    public string? GameFolder { get; set; } // e.g. "rock-paper-scissors"

    [MaxLength(100)]
    public string? GameEntryFile { get; set; } = "index.html"; // e.g. "index.html"

    [MaxLength(500)]
    public string? GameCustomParams { get; set; } // e.g. "mode=auto"

    [MaxLength(30)]
    public string ParticipationMode { get; set; } = "onetime"; // "onetime" | "multipletime"

    public int GapDurationMinutes { get; set; } = 10; // Jeda waktu jika multipletime

    public bool LocationTagDetect { get; set; } = false; // check user location and log (GPS)

    [MaxLength(500)]
    public string? WelcomeMessage { get; set; } = "Selamat Datang di Interaktif DOOH Warna Warni";

    [MaxLength(500)]
    public string? InstructionComment { get; set; } = "Silakan isi nama dan nomor telepon untuk berpartisipasi";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Konfigurasi JSON posisi visual Face Slot dan Speech Banner pada billboard kreatif (khusus PhotoComment).
    /// </summary>
    public string? SlotConfigJson { get; set; }

    public List<DynamicContentQuestion> Questions { get; set; } = new();
}

public class DynamicContentQuestion
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string DynamicContentMasterId { get; set; } = string.Empty;

    [ForeignKey(nameof(DynamicContentMasterId))]
    public DynamicContentMaster? DynamicContentMaster { get; set; }

    [Required]
    [MaxLength(500)]
    public string QuestionText { get; set; } = string.Empty; // Isi pertanyaan

    [Required]
    [MaxLength(50)]
    public string QuestionType { get; set; } = "multiple"; // "multiple", "image_multiple", "essay"

    public string? QuestionImageUrl { get; set; } // Gambar/media pendukung pertanyaan (opsional)

    // Kunci jawaban benar (khusus jika ContentType == BrandIQ / Quiz)
    [MaxLength(10)]
    public string? CorrectOptionKey { get; set; } // "A", "B", "C", "D"

    public int SortOrder { get; set; } = 1;

    public List<DynamicContentOption> Options { get; set; } = new();

    public List<DynamicContentVoteLog> VoteLogs { get; set; } = new();
}

public class DynamicContentOption
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int QuestionId { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public DynamicContentQuestion? Question { get; set; }

    [Required]
    [MaxLength(10)]
    public string OptionKey { get; set; } = string.Empty; // "A", "B", "C", "D"

    [Required]
    [MaxLength(200)]
    public string OptionText { get; set; } = string.Empty; // Teks jawaban

    public string? OptionImageUrl { get; set; } // Icon/gambar opsi jawaban (opsional)

    [MaxLength(20)]
    public string ColorHex { get; set; } = "#3B82F6";

    // Penanda jawaban yang benar (khusus jika ContentType == BrandIQ / Quiz)
    public bool IsCorrect { get; set; } = false;

    public int VoteCount { get; set; } = 0;

    public int SortOrder { get; set; } = 1;
}

public class DynamicContentVoteLog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int QuestionId { get; set; }

    [ForeignKey(nameof(QuestionId))]
    public DynamicContentQuestion? Question { get; set; }

    [Required]
    [MaxLength(100)]
    public string DynamicContentMasterId { get; set; } = string.Empty;

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

    // Audience & Device Telemetry (Untuk Analitik Klien & Pitching)
    [MaxLength(50)]
    public string? City { get; set; } = "Nasional"; // Kota titik LED (Surabaya, Jakarta, Medan, dll)

    [MaxLength(50)]
    public string? DeviceType { get; set; } // Mobile, Tablet, Desktop

    [MaxLength(50)]
    public string? DeviceBrand { get; set; } // Samsung, Apple, Xiaomi, Oppo, Vivo, dll

    [MaxLength(100)]
    public string? DeviceModel { get; set; } // iPhone, Galaxy, Redmi, dll

    [MaxLength(50)]
    public string? OperatingSystem { get; set; } // iOS, Android, Windows, dll

    [MaxLength(50)]
    public string? BrowserName { get; set; } // Chrome, Safari, Samsung Internet, TikTok in-app, dll

    [MaxLength(30)]
    public string? ScreenResolution { get; set; } // e.g. 390x844

    [MaxLength(20)]
    public string? UserLanguage { get; set; } // e.g. id-ID

    public double? TimeToVoteSeconds { get; set; } // Durasi respon sejak buka hingga submit

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    // Controller Input (Name | Phone)
    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    // Geotagging & GPS Location (locationTag detect)
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public bool LocationDetected { get; set; } = false;
}

// DTOs (Data Transfer Objects)
public class DynamicContentVoteRequest
{
    public string? Token { get; set; }

    public string DynamicContentId { get; set; } = string.Empty;

    // Backward-compatibility alias
    public string SurveyId
    {
        get => DynamicContentId;
        set => DynamicContentId = value;
    }

    public int QuestionId { get; set; }

    [Required]
    public string OptionKey { get; set; } = string.Empty;

    public string? TextResponse { get; set; }

    [Required]
    public string UserName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public string SiteId { get; set; } = "Default";

    [Required]
    public string DeviceFingerprint { get; set; } = string.Empty;

    public string? City { get; set; }
    public string? DeviceType { get; set; }
    public string? DeviceBrand { get; set; }
    public string? DeviceModel { get; set; }
    public string? OperatingSystem { get; set; }
    public string? BrowserName { get; set; }
    public string? ScreenResolution { get; set; }
    public string? UserLanguage { get; set; }
    public double? TimeToVoteSeconds { get; set; }
    public string? UserAgent { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class DynamicContentVoteResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool? IsCorrect { get; set; } // null jika Survey, true/false jika BrandIQ (Quiz)
    public string? CorrectOptionKey { get; set; } // Kunci jawaban benar jika BrandIQ
    public string? CorrectOptionText { get; set; } // Teks jawaban benar jika BrandIQ
    public DynamicContentResultDto? CurrentResults { get; set; }
}

public class DynamicContentResultDto
{
    public string DynamicContentId { get; set; } = string.Empty;

    // Backward-compatibility alias
    public string SurveyId
    {
        get => DynamicContentId;
        set => DynamicContentId = value;
    }

    public string ClientName { get; set; } = string.Empty;
    public string CampaignTitle { get; set; } = string.Empty;
    public string? AdHeadline { get; set; }
    public string? AdSubheadline { get; set; }
    public string? BannerImageUrl { get; set; }
    public string? BrandLogoUrl { get; set; }
    public string ThemeColorHex { get; set; } = "#0072FF";
    public string SiteId { get; set; } = string.Empty;
    public int TotalVotes { get; set; }

    public string ContentType { get; set; } = "Survey";
    public string ParticipationMode { get; set; } = "onetime";
    public int GapDurationMinutes { get; set; } = 10;
    public bool LocationTagDetect { get; set; } = false;
    public string? WelcomeMessage { get; set; }
    public string? InstructionComment { get; set; }

    public string? GameFolder { get; set; }
    public string? GameEntryFile { get; set; }
    public string? GameCustomParams { get; set; }

    // Primary question
    public int QuestionId { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string QuestionType { get; set; } = "multiple";
    public string? QuestionImageUrl { get; set; }
    public string? CorrectOptionKey { get; set; } // Kunci jawaban jika Quiz

    public List<DynamicContentOptionResultDto> Options { get; set; } = new();
}

public class DynamicContentOptionResultDto
{
    public int OptionId { get; set; }
    public string OptionKey { get; set; } = string.Empty;
    public string OptionText { get; set; } = string.Empty;
    public string? OptionImageUrl { get; set; }
    public string ColorHex { get; set; } = "#3B82F6";
    public bool IsCorrect { get; set; } = false; // Flag benar jika Quiz
    public int VoteCount { get; set; }
    public double Percentage { get; set; }
}

public class PhotoCommentSubmitRequest
{
    [Required]
    public string ContentId { get; set; } = string.Empty;
    public string? Token { get; set; }
    public string SiteId { get; set; } = "Default";

    [Required(ErrorMessage = "Nama wajib diisi.")]
    [MaxLength(100)]
    public string UserName { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Komentar wajib diisi.")]
    [MaxLength(280, ErrorMessage = "Komentar maksimal 280 karakter.")]
    public string CommentText { get; set; } = string.Empty;

    [Required(ErrorMessage = "Foto wajib diambil.")]
    public string PhotoBase64 { get; set; } = string.Empty; // Base64 Data URL (image/jpeg / image/png)

    [Required]
    public string DeviceFingerprint { get; set; } = string.Empty;

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? City { get; set; }
    public string? DeviceBrand { get; set; }
    public string? DeviceModel { get; set; }
    public string? OperatingSystem { get; set; }
    public string? BrowserName { get; set; }
}

public class PhotoCommentItemDto
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string CommentText { get; set; } = string.Empty;
    public string PhotoUrl { get; set; } = string.Empty;
    public string SiteId { get; set; } = string.Empty;
    public string TimeAgo { get; set; } = "Baru saja";
    public DateTime PostedAt { get; set; }
}

public class PhotoCommentFeedDto
{
    public string ContentId { get; set; } = string.Empty;
    public string CampaignTitle { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string? BannerImageUrl { get; set; }
    public string? BrandLogoUrl { get; set; }
    public string? WelcomeMessage { get; set; }
    public string? InstructionComment { get; set; }
    public string SiteId { get; set; } = string.Empty;
    public int TotalParticipants { get; set; }
    public int TotalPhotos { get; set; }
    public List<PhotoCommentItemDto> RecentPhotos { get; set; } = new();
}

public class PhotoCommentWaitTimeResponse
{
    public bool CanPost { get; set; } = true;
    public int RemainingMinutes { get; set; } = 0;
    public int RemainingSeconds { get; set; } = 0;
    public string Message { get; set; } = string.Empty;
}

public class PhotoCommentSlotConfig
{
    // Mode pemilihan: "auto" (AI) atau "manual"
    public string DetectionMode { get; set; } = "auto";

    // Slot Wajah (dalam persentase % dari ukuran kanvas billboard)
    public double FaceX { get; set; } = 77.08;
    public double FaceY { get; set; } = 1.79;
    public double FaceWidth { get; set; } = 13.33;
    public double FaceHeight { get; set; } = 39.29;
    public string FaceShape { get; set; } = "ellipse"; // "ellipse" | "circle" | "rounded"

    // Slot Balon / Banner Ucapan (dalam persentase %)
    public double SpeechX { get; set; } = 49.00;
    public double SpeechY { get; set; } = 66.43;
    public double SpeechWidth { get; set; } = 48.33;
    public double SpeechHeight { get; set; } = 28.93;
    public string SpeechBgColor { get; set; } = "#FEE87D";
    public string SpeechTextColor { get; set; } = "#1E293B";

    // Gambar overlay tubuh/kerah depan (opsional)
    public string? CollarOverlayUrl { get; set; }
    // Gambar panduan siluet untuk kamera HP (opsional)
    public string? BodyGuideUrl { get; set; }
}
