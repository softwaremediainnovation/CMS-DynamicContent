using CmsBillboard.DynamicContent.Models;

namespace CmsBillboard.DynamicContent.Data;

public static class DbInitializer
{
    public static void SeedVoteLogsIfEmpty(AppDbContext context)
    {
        SeedBrandGeniusIfEmpty(context);

        // Only seed if no vote logs exist for samsung-zfold5
        if (context.DynamicContentVoteLogs.Any(v => v.DynamicContentMasterId == "samsung-zfold5"))
        {
            return;
        }

        var rand = new Random(42);

        // 47 A, 33 B, 10 C, 10 D
        var options = new List<string>();
        for (int i = 0; i < 47; i++) options.Add("A");
        for (int i = 0; i < 33; i++) options.Add("B");
        for (int i = 0; i < 10; i++) options.Add("C");
        for (int i = 0; i < 10; i++) options.Add("D");

        for (int i = options.Count - 1; i > 0; i--)
        {
            int k = rand.Next(i + 1);
            var temp = options[i];
            options[i] = options[k];
            options[k] = temp;
        }

        var samsungModels = new[] { "Galaxy S24 Ultra", "Galaxy S23 5G", "Galaxy Z Fold5", "Galaxy Z Flip5", "Galaxy A54 5G", "Galaxy A34 5G", "Galaxy S21 FE", "Galaxy A14" };
        var appleModels = new[] { "iPhone 15 Pro", "iPhone 15", "iPhone 14 Pro Max", "iPhone 14", "iPhone 13", "iPhone 12", "iPhone 11" };
        var xiaomiModels = new[] { "Redmi Note 13 Pro", "Xiaomi 13T", "POCO F5", "Redmi 12" };
        var oppoModels = new[] { "Oppo Reno 11 5G", "Oppo Find N3 Flip", "Oppo A78" };
        var vivoModels = new[] { "Vivo V30 5G", "Vivo Y27 5G" };

        var devices = new List<(string Brand, string Model, string Os, string Browser)>();
        for (int i = 0; i < 48; i++)
        {
            devices.Add(("Samsung", samsungModels[rand.Next(samsungModels.Length)], "Android", rand.Next(2) == 0 ? "Samsung Internet" : "Chrome Mobile"));
        }
        for (int i = 0; i < 32; i++)
        {
            devices.Add(("Apple", appleModels[rand.Next(appleModels.Length)], "iOS", rand.Next(10) < 8 ? "Mobile Safari" : "Chrome Mobile"));
        }
        for (int i = 0; i < 11; i++)
        {
            devices.Add(("Xiaomi", xiaomiModels[rand.Next(xiaomiModels.Length)], "Android", "Chrome Mobile"));
        }
        for (int i = 0; i < 6; i++)
        {
            devices.Add(("Oppo", oppoModels[rand.Next(oppoModels.Length)], "Android", "Chrome Mobile"));
        }
        for (int i = 0; i < 3; i++)
        {
            devices.Add(("Vivo", vivoModels[rand.Next(vivoModels.Length)], "Android", "Chrome Mobile"));
        }

        for (int i = devices.Count - 1; i > 0; i--)
        {
            int k = rand.Next(i + 1);
            var temp = devices[i];
            devices[i] = devices[k];
            devices[k] = temp;
        }

        var names = new[]
        {
            "Rizky Pratama", "Dimas Setiawan", "Budi Santoso", "Siti Nurhaliza", "Agus Hermawan",
            "Dewi Lestari", "Bayu Wicaksono", "Anisa Rahmawati", "Fajar Nugraha", "Mega Utami",
            "Eko Prasetyo", "Indah Permata", "Rian Saputra", "Nadia Safitri", "Hendra Gunawan",
            "Tri Wahyuni", "Dian Sastro", "Bambang Pamungkas", "Maya Anggraini", "Aditya Putra",
            "Gilang Ramadhan", "Kartika Sari", "Arif Hidayat", "Putri Wulandari", "Yusuf Maulana",
            "Fitri Handayani", "Reza Rahadian", "Ayu Tingting", "Wahyu Hidayat", "Citra Kirana",
            "Denny Caknan", "Tiara Andini", "Lyodra Ginting", "Ziva Magnolya", "Raffi Ahmad",
            "Nagita Slavina", "Atta Halilintar", "Aurel Hermansyah", "Deddy Corbuzier", "Gading Marten"
        };

        var sites = new[]
        {
            ("SBY-Basuki-Rahmat-01", "Surabaya", -7.2625, 112.7428),
            ("SBY-Basuki-Rahmat-01", "Surabaya", -7.2625, 112.7428),
            ("SBY-Panglima-Sudirman-01", "Surabaya", -7.2712, 112.7441),
            ("JKT-Thamrin-01", "Jakarta", -6.1944, 106.8229),
            ("JKT-Gatot-Subroto-02", "Jakarta", -6.2301, 106.8198),
            ("BDG-Asia-Afrika-01", "Bandung", -6.9215, 107.6074)
        };

        var baseTime = DateTime.UtcNow.AddHours(-36);
        var logs = new List<DynamicContentVoteLog>();

        for (int idx = 0; idx < 100; idx++)
        {
            var optKey = options[idx];
            var dev = devices[idx];
            var st = sites[rand.Next(sites.Length)];
            string name = (rand.Next(10) > 2) ? names[rand.Next(names.Length)] : "Anonim";
            string? phone = (name != "Anonim") ? "08" + rand.Next(11, 99) + rand.Next(1000000, 9999999).ToString() : null;
            string res = dev.Os == "iOS" ? "390x844" : (rand.Next(2) == 0 ? "412x915" : "360x800");
            double timeToVote = Math.Round(3.5 + (rand.NextDouble() * 8.5), 1);
            var votedAt = baseTime.AddMinutes(rand.Next(1, 2100));

            logs.Add(new DynamicContentVoteLog
            {
                QuestionId = 1,
                DynamicContentMasterId = "samsung-zfold5",
                OptionKey = optKey,
                TextResponse = null,
                PhotoUrl = null,
                UserName = name,
                SiteId = st.Item1,
                DeviceFingerprint = Guid.NewGuid().ToString("N"),
                IpAddress = $"114.125.{rand.Next(10, 250)}.{rand.Next(1, 254)}",
                VotedAt = votedAt,
                City = st.Item2,
                DeviceType = "Mobile",
                DeviceBrand = dev.Brand,
                DeviceModel = dev.Model,
                OperatingSystem = dev.Os,
                BrowserName = dev.Browser,
                ScreenResolution = res,
                UserLanguage = "id-ID",
                TimeToVoteSeconds = timeToVote,
                UserAgent = $"Mozilla/5.0 ({dev.Os}; Mobile) AppleWebKit/537.36",
                PhoneNumber = phone,
                Latitude = st.Item3,
                Longitude = st.Item4,
                LocationDetected = true
            });
        }

        context.DynamicContentVoteLogs.AddRange(logs);
        context.SaveChanges();
    }

    public static void SeedBrandGeniusIfEmpty(AppDbContext context)
    {
        // Seed default Brand Genius Demo (Indomie / Brand Genius Game) if not exists
        if (!context.DynamicContentMasters.Any(m => m.Id == "brand-genius-demo"))
        {
            var geniusMaster = new DynamicContentMaster
            {
                Id = "brand-genius-demo",
                ClientName = "Indomie (PT Indofood CBP)",
                CampaignTitle = "BRAND GENIUS: TEBAK LOGO & SLOGAN RESMI",
                AdHeadline = "LEGENDA RASA NUSANTARA",
                AdSubheadline = "Perhatikan layar videotron! Tebak potongan logo atau kelanjutan slogan resmi dengan tepat.",
                BannerImageUrl = "/images/samsung_galaxy_fold.svg",
                BrandLogoUrl = null,
                ThemeColorHex = "#F59E0B",
                ContentType = "BrandGenius",
                ParticipationMode = "multipletime",
                GapDurationMinutes = 1,
                LocationTagDetect = false,
                WelcomeMessage = "Selamat Datang di Brand Genius Game! Perhatikan layar LED di depan Anda.",
                InstructionComment = "Pilih jawaban yang benar dari potongan logo di layar videotron.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            context.DynamicContentMasters.Add(geniusMaster);
            context.SaveChanges();

            var geniusQuestion = new DynamicContentQuestion
            {
                DynamicContentMasterId = "brand-genius-demo",
                QuestionText = "Lengkapi potongan logo / slogan resmi brand yang tertera pada layar Videotron:",
                QuestionType = "multiple",
                CorrectOptionKey = "A",
                SortOrder = 1
            };
            context.DynamicContentQuestions.Add(geniusQuestion);
            context.SaveChanges();

            var geniusOptions = new List<DynamicContentOption>
            {
                new DynamicContentOption { QuestionId = geniusQuestion.Id, OptionKey = "A", OptionText = "Indomie Seleraku", ColorHex = "#10B981", IsCorrect = true, VoteCount = 42, SortOrder = 1 },
                new DynamicContentOption { QuestionId = geniusQuestion.Id, OptionKey = "B", OptionText = "Indomie Kebanggaanku", ColorHex = "#3B82F6", IsCorrect = false, VoteCount = 12, SortOrder = 2 },
                new DynamicContentOption { QuestionId = geniusQuestion.Id, OptionKey = "C", OptionText = "Indomie Selalu Ada", ColorHex = "#F59E0B", IsCorrect = false, VoteCount = 8, SortOrder = 3 },
                new DynamicContentOption { QuestionId = geniusQuestion.Id, OptionKey = "D", OptionText = "Indomie Paling Enak", ColorHex = "#EF4444", IsCorrect = false, VoteCount = 5, SortOrder = 4 }
            };
            context.DynamicContentOptions.AddRange(geniusOptions);
            context.SaveChanges();
        }
    }
}
