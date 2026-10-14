using System.Text.RegularExpressions;

namespace CmsBillboard.DynamicContent.Services;

public class ContentModerationService : IContentModerationService
{
    private readonly ILogger<ContentModerationService> _logger;

    // 1. Regex untuk SARA & Ujaran Kebencian
    private static readonly Regex SaraRegex = new(
        @"\b(kafir|cokin|antek\s*(pki|yahudi)|teroris|sesat|aliran\s*sesat|kristenisasi|islamisasi|pribumi\s*asli|dasar\s*(cina|papua|jawa|batak|madura|sunda))\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 2. Regex untuk Kata Kasar / Profanity / Pornografi
    private static readonly Regex ProfanityRegex = new(
        @"\b(anjing|anjir|anjrit|bangsat|babi|kontol|memek|jembut|itil|pepek|pantek|peler|perek|lonte|pelacur|ngentot|ngewe|bokep|porno|fuck|shit|bitch|bastard|asshole|dick|pussy|jancok|jancuk|dancok|asu|bajingan|tolol|bego|idiot)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // 3. Regex untuk Promosi Spam, Judi/Slot, Pinjol, & Tautan Ilegal
    private static readonly Regex SpamPromoRegex = new(
        @"(https?:\/\/|www\.|bit\.ly|t\.me|wa\.me|wa\.link|\.com|\.id|\.net|\.xyz)|" +
        @"\b(slot|gacor|maxwin|pragmatic|zeus|olympus|togel|judi|casino|chip|poker|open\s*bo|pinjol|dana\s*kaget|link\s*di\s*bio|ready\s*stock|order\s*sekarang|hubungi\s*wa|promo\s*murah|diskon\s*besar)\b|" +
        @"(?:\+?62|0)8[1-9][0-9]{1,3}[-.\s]?[0-9]{3,4}[-.\s]?[0-9]{3,5}",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public ContentModerationService(ILogger<ContentModerationService> logger)
    {
        _logger = logger;
    }

    public ModerationCheckResult CheckComment(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return new ModerationCheckResult
            {
                IsApproved = false,
                Reason = "Komentar tidak boleh kosong.",
                Category = "EMPTY",
                SanitizedText = string.Empty
            };
        }

        var cleaned = comment.Trim();

        // Batas panjang teks wajar untuk videotron
        if (cleaned.Length > 280)
        {
            return new ModerationCheckResult
            {
                IsApproved = false,
                Reason = "Komentar maksimal 280 karakter agar terbaca jelas di layar videotron.",
                Category = "LENGTH_EXCEEDED",
                SanitizedText = cleaned[..280]
            };
        }

        // Cek 1: SARA
        var saraMatch = SaraRegex.Match(cleaned);
        if (saraMatch.Success)
        {
            _logger.LogWarning("Komentar ditolak karena SARA: Matched '{Match}' in '{Text}'", saraMatch.Value, cleaned);
            return new ModerationCheckResult
            {
                IsApproved = false,
                Reason = "Komentar Anda tidak dapat ditayangkan karena mengandung unsur SARA atau ujaran kebencian.",
                Category = "SARA",
                MatchedTerm = saraMatch.Value,
                SanitizedText = cleaned
            };
        }

        // Cek 2: Kata Kasar / Profanity
        var profanityMatch = ProfanityRegex.Match(cleaned);
        if (profanityMatch.Success)
        {
            _logger.LogWarning("Komentar ditolak karena kata kasar: Matched '{Match}' in '{Text}'", profanityMatch.Value, cleaned);
            return new ModerationCheckResult
            {
                IsApproved = false,
                Reason = "Komentar Anda terdeteksi mengandung kata-kata yang tidak pantas untuk penayangan publik.",
                Category = "PROFANITY",
                MatchedTerm = profanityMatch.Value,
                SanitizedText = cleaned
            };
        }

        // Cek 3: Promosi Spam / Nomor HP / Tautan Liar
        var spamMatch = SpamPromoRegex.Match(cleaned);
        if (spamMatch.Success)
        {
            _logger.LogWarning("Komentar ditolak karena promosi spam: Matched '{Match}' in '{Text}'", spamMatch.Value, cleaned);
            return new ModerationCheckResult
            {
                IsApproved = false,
                Reason = "Komentar Anda terdeteksi mengandung unsur promosi komersial liar, nomor kontak, atau tautan.",
                Category = "SPAM_PROMO",
                MatchedTerm = spamMatch.Value,
                SanitizedText = cleaned
            };
        }

        return new ModerationCheckResult
        {
            IsApproved = true,
            Reason = "Komentar mematuhi standar etika penayangan videotron publik.",
            SanitizedText = cleaned
        };
    }
}
