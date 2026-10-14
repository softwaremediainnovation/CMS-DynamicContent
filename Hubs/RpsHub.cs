using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace CmsBillboard.DynamicContent.Hubs;

public class RpsPlayer
{
    public string ConnectionId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string Choice { get; set; } = string.Empty; // "rock", "paper", "scissors"
    public int Score { get; set; } = 0;
    public bool IsBot { get; set; } = false;
}

public class RpsRoom
{
    public string RoomId { get; set; } = string.Empty;
    public readonly object Lock = new();

    // Layar Videotron di Ruang Ini (Mendukung multi-koneksi / re-connect)
    public HashSet<string> ScreenConnectionIds { get; } = new();
    public bool IsScreenPaused { get; set; } = false;

    // Pemain Aktif di Arena (Maksimal 2)
    public RpsPlayer? Player1 { get; set; } = null;
    public RpsPlayer? Player2 { get; set; } = null;

    // Antrean Pemain ke-3 dan seterusnya
    public List<RpsPlayer> Queue { get; } = new();

    // Sesi Pertandingan
    public int CurrentRound { get; set; } = 0;
    public const int BaseRounds = 3;
    public bool IsRoundActive { get; set; } = false;
    public bool IsMatchOverPending { get; set; } = false;
    public bool IsRoundOverPending { get; set; } = false;

    // Timers
    public Timer? BotMatchTimer { get; set; } = null;
    public Timer? RoundTimeoutTimer { get; set; } = null;
}

public class RpsHub : Hub
{
    private readonly IHubContext<RpsHub> _hubContext;
    private readonly ILogger<RpsHub> _logger;
    private readonly Microsoft.Extensions.DependencyInjection.IServiceScopeFactory _scopeFactory;

    // Manajemen Room / Layar Independen
    private static readonly ConcurrentDictionary<string, RpsRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> _connectionToRoom = new();
    private static readonly ConcurrentDictionary<string, bool> _screenConnections = new();

    public RpsHub(IServiceProvider sp, ILogger<RpsHub> logger)
    {
        _hubContext = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<IHubContext<RpsHub>>(sp);
        _logger = logger;
        _scopeFactory = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>(sp);
    }

    private void LogAnalytics(string roomId, string eventType, string details)
    {
        _logger.LogInformation("[RPS Log] Room: {RoomId}, Event: {EventType}, Details: {Details}", roomId, eventType, details);
    }

    private static RpsRoom GetOrCreateRoom(string roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            roomId = "default";
        }
        roomId = roomId.Trim();
        return _rooms.GetOrAdd(roomId, id => new RpsRoom { RoomId = id });
    }

    // ============================================================
    // A. REGISTRASI LAYAR VIDEOTRON (Mendukung Room / Screen ID)
    // ============================================================
    public async Task RegisterScreen(string? roomId = null)
    {
        string targetRoom = string.IsNullOrWhiteSpace(roomId) ? "default" : roomId.Trim();
        var room = GetOrCreateRoom(targetRoom);

        _connectionToRoom[Context.ConnectionId] = room.RoomId;
        _screenConnections[Context.ConnectionId] = true;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"Screen_{room.RoomId}");

        bool wasPaused = false;
        RpsPlayer? p1 = null;
        RpsPlayer? p2 = null;
        int currentRound = 0;

        lock (room.Lock)
        {
            room.ScreenConnectionIds.Add(Context.ConnectionId);
            if (room.IsScreenPaused)
            {
                room.IsScreenPaused = false;
                wasPaused = true;
            }
            p1 = room.Player1;
            p2 = room.Player2;
            currentRound = room.CurrentRound;
        }

        _logger.LogInformation("[SCREEN] Videotron terhubung ke Room [{Room}]. ConnId: {ConnId}", room.RoomId, Context.ConnectionId);

        if (p1 != null && p2 != null)
        {
            if (wasPaused)
            {
                // Kasus 1: Selesai Jeda Iklan -> Lanjutkan Game (Resume)
                await Clients.Caller.SendAsync("GameResumeForScreen",
                    p1.PlayerName, p1.Score,
                    p2.PlayerName, p2.Score,
                    currentRound
                );

                if (!p1.IsBot) await _hubContext.Clients.Client(p1.ConnectionId).SendAsync("GameResumedByScreen");
                if (!p2.IsBot) await _hubContext.Clients.Client(p2.ConnectionId).SendAsync("GameResumedByScreen");

                // Jika sedang menunggu transisi akhir pertandingan, BYPASS saja langsung selesaikan!
                if (room.IsMatchOverPending)
                {
                    room.IsMatchOverPending = false; 
                    _ = Task.Run(async () => await EndMatchAndAdvance(room));
                    return;
                }

                // Jika sedang menunggu transisi ronde biasa, BYPASS langsung lanjut ronde!
                if (room.IsRoundOverPending)
                {
                    room.IsRoundOverPending = false;
                    _ = Task.Run(async () => await StartRound(room));
                    return;
                }

                // Kickstart ulang timer/ronde yang terhenti
                if (room.IsRoundActive)
                {
                    StartRoundTimeoutTimer(room);
                }
                else if (currentRound == 0)
                {
                    // Belum mulai sama sekali karena P2 join atau naik dari antrean saat jeda iklan!
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        await StartRound(room);
                    });
                }
                // Jika round > 0 tapi tidak aktif, berarti loop penundaan di CalculateRoundResult 
                // sedang bekerja, biarkan ia melanjutkannya secara otomatis.
            }
            else
            {
                // Sedang ada match aktif (layar baru join tanpa ada jeda sebelumnya)
                await Clients.Caller.SendAsync("MatchInProgress", p1.PlayerName, p1.Score, p2.PlayerName, p2.Score, currentRound);
            }
        }
        else if (p1 != null && p2 == null)
        {
            // Baru Player 1 yang masuk (atau sedang nunggu bot)
            await Clients.Caller.SendAsync("LobbyState", p1.PlayerName, null);
            if (wasPaused)
            {
                if (!p1.IsBot) await _hubContext.Clients.Client(p1.ConnectionId).SendAsync("GameResumedByScreen");
                StartBotWaitTimer(room);
            }
        }
        else
        {
            // Belum ada pemain sama sekali -> Tampilkan QR Code Standby
            await Clients.Caller.SendAsync("NoActivePlayer");
        }
    }

    // ============================================================
    // B. PENDAFTARAN PEMAIN VIA HP
    // ============================================================
    public Task JoinGameDefault(string playerName) => JoinGameInternal("default", playerName);

    public Task JoinGame(string roomId, string playerName) => JoinGameInternal(roomId, playerName);

    private async Task JoinGameInternal(string? roomId, string playerName)
    {
        string targetRoom = string.IsNullOrWhiteSpace(roomId) ? "default" : roomId.Trim();

        // Validasi jika roomId adalah encrypted security token
        if (!string.IsNullOrWhiteSpace(roomId) && roomId.Length >= 30)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var tokenService = scope.ServiceProvider.GetService<CmsBillboard.DynamicContent.Services.ISecurityTokenService>();
                if (tokenService != null)
                {
                    var (isValid, payload, _) = tokenService.ValidateVoteToken(roomId);
                    if (isValid && payload != null && !string.IsNullOrWhiteSpace(payload.SiteId))
                    {
                        targetRoom = payload.SiteId.Trim().ToUpper();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Gagal memvalidasi token saat JoinGame: {Err}", ex.Message);
            }
        }

        var room = GetOrCreateRoom(targetRoom);

        _connectionToRoom[Context.ConnectionId] = room.RoomId;

        RpsPlayer newPlayer = new()
        {
            ConnectionId = Context.ConnectionId,
            PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Pemain" : playerName.Trim()
        };

        bool assignedP1 = false;
        bool assignedP2 = false;
        int queuePosition = 0;
        bool screenIsPaused = false;

        lock (room.Lock)
        {
            screenIsPaused = room.IsScreenPaused;

            if (room.Player1 == null)
            {
                room.Player1 = newPlayer;
                assignedP1 = true;
            }
            else if (room.Player2 == null && room.Player1.ConnectionId != Context.ConnectionId)
            {
                room.Player2 = newPlayer;
                assignedP2 = true;
            }
            else
            {
                // Sudah ada 2 pemain aktif -> Masuk Antrean Tunggu
                room.Queue.Add(newPlayer);
                queuePosition = room.Queue.Count;
            }
        }

        _logger.LogInformation("[PLAYER-JOIN] {Player} masuk Room [{Room}] (ConnId: {ConnId}, P1: {P1}, P2: {P2})",
            newPlayer.PlayerName, room.RoomId, newPlayer.ConnectionId, assignedP1, assignedP2);

        LogAnalytics(room.RoomId, "PLAYER_JOINED", $"Player Name: {newPlayer.PlayerName}");

        if (assignedP1)
        {
            await Clients.Caller.SendAsync("AssignedRole", "Player 1", newPlayer.PlayerName);
            await BroadcastLobbyState(room);

            // Mulai Timer 5 Detik: Jika Player 2 belum ada, buat BOT
            StartBotWaitTimer(room);

            if (screenIsPaused)
            {
                await Clients.Caller.SendAsync("GamePausedByScreen");
            }
        }
        else if (assignedP2)
        {
            // Player 2 berhasil masuk -> Batalkan timer BOT
            StopBotWaitTimer(room);

            await Clients.Caller.SendAsync("AssignedRole", "Player 2", newPlayer.PlayerName);
            await BroadcastLobbyState(room);

            if (screenIsPaused)
            {
                await Clients.Caller.SendAsync("GamePausedByScreen");
            }
            else
            {
                // Kedua pemain sudah siap, mulai ronde 1
                await Task.Delay(1000);
                await StartRound(room);
            }
        }
        else
        {
            // Informasikan posisi antrean ke pemain
            _logger.LogInformation("[QUEUE-JOIN] {Player} masuk antrean #{Pos} di Room [{Room}] (ConnId: {ConnId})", newPlayer.PlayerName, queuePosition, room.RoomId, newPlayer.ConnectionId);
            LogAnalytics(room.RoomId, "PLAYER_QUEUED", $"Player Name: {newPlayer.PlayerName} (Position: {queuePosition})");

            await Clients.Caller.SendAsync("QueueWaiting", queuePosition, newPlayer.PlayerName);
        }
    }

    // ============================================================
    // C. INPUT PILIHAN DARI HP (ROCK / PAPER / SCISSORS)
    // ============================================================
    public Task SubmitChoiceDefault(string choice) => SubmitChoiceInternal(null, choice);

    public Task SubmitChoice(string roomId, string choice) => SubmitChoiceInternal(roomId, choice);

    private async Task SubmitChoiceInternal(string? roomId, string choice)
    {
        choice = choice.ToLower().Trim();
        if (choice != "rock" && choice != "paper" && choice != "scissors") return;

        if (string.IsNullOrWhiteSpace(roomId))
        {
            _connectionToRoom.TryGetValue(Context.ConnectionId, out roomId);
        }
        if (string.IsNullOrWhiteSpace(roomId)) roomId = "default";

        if (!_rooms.TryGetValue(roomId, out var room)) return;

        bool bothReady = false;
        lock (room.Lock)
        {
            if (room.IsScreenPaused || !room.IsRoundActive) return;

            if (room.Player1 != null && room.Player1.ConnectionId == Context.ConnectionId)
            {
                room.Player1.Choice = choice;
            }
            else if (room.Player2 != null && room.Player2.ConnectionId == Context.ConnectionId)
            {
                room.Player2.Choice = choice;
            }

            // Jika lawan adalah BOT, bot langsung memilih acak
            if (room.Player2 != null && room.Player2.IsBot && string.IsNullOrEmpty(room.Player2.Choice))
            {
                string[] botOptions = { "rock", "paper", "scissors" };
                room.Player2.Choice = botOptions[Random.Shared.Next(botOptions.Length)];
            }

            // Cek apakah kedua pemain sudah memilih
            if (room.Player1 != null && !string.IsNullOrEmpty(room.Player1.Choice) &&
                room.Player2 != null && !string.IsNullOrEmpty(room.Player2.Choice))
            {
                bothReady = true;
                room.IsRoundActive = false;
            }
        }

        if (bothReady)
        {
            StopRoundTimeoutTimer(room);
            await CalculateRoundResult(room);
        }
    }

    // ============================================================
    // D. LOGIKA PERMAINAN & RONDE
    // ============================================================
    private async Task StartRound(RpsRoom room)
    {
        lock (room.Lock)
        {
            room.CurrentRound++;
            room.IsRoundActive = true;
            room.IsRoundOverPending = false; // Bersihkan status transisi ronde
            if (room.Player1 != null) room.Player1.Choice = "";
            if (room.Player2 != null) room.Player2.Choice = "";
        }

        _logger.LogInformation("[ROUND-START] Room [{Room}] Ronde {Round} dimulai: {P1} vs {P2}",
            room.RoomId, room.CurrentRound, room.Player1?.PlayerName, room.Player2?.PlayerName);

        // 1. Beritahu videotron di room ini untuk masuk ke ronde baru
        await _hubContext.Clients.Group($"Screen_{room.RoomId}").SendAsync("RoundStarted",
            room.Player1?.PlayerName, room.Player1?.Score,
            room.Player2?.PlayerName, room.Player2?.Score,
            room.CurrentRound
        );

        // 2. Buka tombol pemilihan di HP masing-masing pemain
        if (room.Player1 != null && !room.Player1.IsBot)
        {
            await _hubContext.Clients.Client(room.Player1.ConnectionId).SendAsync("ReadyToChoose", room.CurrentRound);
        }
        if (room.Player2 != null && !room.Player2.IsBot)
        {
            await _hubContext.Clients.Client(room.Player2.ConnectionId).SendAsync("ReadyToChoose", room.CurrentRound);
        }

        // Timer 5 detik untuk memilih sebelum timeout
        StartRoundTimeoutTimer(room);
    }

    private async Task CalculateRoundResult(RpsRoom room)
    {
        RpsPlayer? p1;
        RpsPlayer? p2;
        int round;

        lock (room.Lock)
        {
            p1 = room.Player1;
            p2 = room.Player2;
            round = room.CurrentRound;
        }

        if (p1 == null || p2 == null) return;

        string c1 = p1.Choice;
        string c2 = p2.Choice;
        string roundWinner = "draw"; 

        if (c1 == c2)
        {
            roundWinner = "draw";
        }
        else if ((c1 == "rock" && c2 == "scissors") ||
                 (c1 == "paper" && c2 == "rock") ||
                 (c1 == "scissors" && c2 == "paper"))
        {
            roundWinner = "p1";
            p1.Score++;
        }
        else
        {
            roundWinner = "p2";
            p2.Score++;
        }

        bool isMatchOver = (round >= RpsRoom.BaseRounds && p1.Score != p2.Score);
        string matchWinnerName = "";

        if (isMatchOver)
        {
            matchWinnerName = p1.Score > p2.Score ? p1.PlayerName : p2.PlayerName;
            LogAnalytics(room.RoomId, "MATCH_FINISHED", $"Winner: {matchWinnerName} | {p1.PlayerName} ({p1.Score}) vs {p2.PlayerName} ({p2.Score})");
        }

        _logger.LogInformation("[ROUND-RESULT] Room [{Room}] Ronde {Round} Selesai: {P1}({C1}) vs {P2}({C2}) => Pemenang: {Winner} | MatchOver: {IsOver}",
            room.RoomId, round, p1.PlayerName, c1, p2.PlayerName, c2, roundWinner, isMatchOver);

        // Kirim hasil ke Layar Videotron di room ini
        await _hubContext.Clients.Group($"Screen_{room.RoomId}").SendAsync("RoundResult",
            p1.PlayerName, c1, p1.Score,
            p2.PlayerName, c2, p2.Score,
            roundWinner,
            isMatchOver,
            matchWinnerName
        );

        // Kirim hasil ke HP Pemain
        if (!p1.IsBot)
        {
            await _hubContext.Clients.Client(p1.ConnectionId).SendAsync("RoundResultForPlayer", roundWinner == "p1" ? "WIN" : (roundWinner == "p2" ? "LOSE" : "DRAW"), isMatchOver);
        }
        if (!p2.IsBot)
        {
            await _hubContext.Clients.Client(p2.ConnectionId).SendAsync("RoundResultForPlayer", roundWinner == "p2" ? "WIN" : (roundWinner == "p1" ? "LOSE" : "DRAW"), isMatchOver);
        }

        if (isMatchOver)
        {
            room.IsMatchOverPending = true;
            _ = Task.Run(async () =>
            {
                // Jeda 5 detik standar (Namun waktu akan membeku jika iklan muncul)
                for (int i = 0; i < 50; i++)
                {
                    while (room.IsScreenPaused) { await Task.Delay(500); }
                    if (!room.IsMatchOverPending) return; // DIBATALKAN OLEH PROSES LAIN (misal Disconnect)
                    await Task.Delay(100);
                }
                if (room.IsMatchOverPending) await EndMatchAndAdvance(room);
            });
        }
        else
        {
            room.IsRoundOverPending = true;
            _ = Task.Run(async () =>
            {
                // Jeda 3 detik standar (Membeku jika iklan muncul)
                for (int i = 0; i < 30; i++)
                {
                    while (room.IsScreenPaused) { await Task.Delay(500); }
                    if (!room.IsRoundOverPending) return; // DIBATALKAN KARENA RESUME
                    await Task.Delay(100);
                }
                if (room.IsRoundOverPending) await StartRound(room);
            });
        }
    }

    public Task TriggerNextRoundDefault() => TriggerNextRoundInternal(null);

    public Task TriggerNextRound(string roomId) => TriggerNextRoundInternal(roomId);

    private async Task TriggerNextRoundInternal(string? roomId)
    {
        if (string.IsNullOrWhiteSpace(roomId))
        {
            _connectionToRoom.TryGetValue(Context.ConnectionId, out roomId);
        }
        if (string.IsNullOrWhiteSpace(roomId)) roomId = "default";

        if (!_rooms.TryGetValue(roomId, out var room)) return;

        lock (room.Lock)
        {
            if (room.IsScreenPaused || room.IsRoundActive) return;
        }
        await StartRound(room);
    }

    private async Task EndMatchAndAdvance(RpsRoom room)
    {
        StopRoundTimeoutTimer(room);
        StopBotWaitTimer(room);

        RpsPlayer? oldP1 = null;
        RpsPlayer? oldP2 = null;

        lock (room.Lock)
        {
            oldP1 = room.Player1;
            oldP2 = room.Player2;
            room.Player1 = null;
            room.Player2 = null;
            room.CurrentRound = 0;
            room.IsRoundActive = false;
            room.IsMatchOverPending = false; // Batalkan loop yang mungkin masih berjalan!

            // Ambil dari antrean jika ada pemain berikutnya
            if (room.Queue.Count > 0)
            {
                room.Player1 = room.Queue[0];
                room.Queue.RemoveAt(0);
            }
            if (room.Queue.Count > 0)
            {
                room.Player2 = room.Queue[0];
                room.Queue.RemoveAt(0);
            }
        }

        // Beritahu pemain lama bahwa sesi telah berakhir dengan mengirimkan hasil akhirnya!
        if (oldP1 != null && !oldP1.IsBot)
        {
            string statusP1 = oldP2 == null || oldP1.Score > oldP2.Score ? "MENANG" : (oldP1.Score < oldP2.Score ? "KALAH" : "SERI");
            string enemyName = oldP2 != null ? oldP2.PlayerName : "BOT";
            int enemyScore = oldP2 != null ? oldP2.Score : 0;
            await _hubContext.Clients.Client(oldP1.ConnectionId).SendAsync("MatchEnded", statusP1, oldP1.PlayerName, enemyName, oldP1.Score, enemyScore);
        }
        
        if (oldP2 != null && !oldP2.IsBot)
        {
            string statusP2 = oldP1 == null || oldP2.Score > oldP1.Score ? "MENANG" : (oldP2.Score < oldP1.Score ? "KALAH" : "SERI");
            string enemyName = oldP1 != null ? oldP1.PlayerName : "BOT";
            int enemyScore = oldP1 != null ? oldP1.Score : 0;
            await _hubContext.Clients.Client(oldP2.ConnectionId).SendAsync("MatchEnded", statusP2, oldP2.PlayerName, enemyName, oldP2.Score, enemyScore);
        }

        await BroadcastQueuePositions(room);

        // Cek kondisi pemain baru di arena
        if (room.Player1 != null && room.Player2 != null)
        {
            await _hubContext.Clients.Client(room.Player1.ConnectionId).SendAsync("AssignedRole", "Player 1", room.Player1.PlayerName);
            await _hubContext.Clients.Client(room.Player2.ConnectionId).SendAsync("AssignedRole", "Player 2", room.Player2.PlayerName);
            await BroadcastLobbyState(room);

            if (room.IsScreenPaused)
            {
                await _hubContext.Clients.Client(room.Player1.ConnectionId).SendAsync("GamePausedByScreen");
                await _hubContext.Clients.Client(room.Player2.ConnectionId).SendAsync("GamePausedByScreen");
            }
            else
            {
                await Task.Delay(1500);
                await StartRound(room);
            }
        }
        else if (room.Player1 != null && room.Player2 == null)
        {
            await _hubContext.Clients.Client(room.Player1.ConnectionId).SendAsync("AssignedRole", "Player 1", room.Player1.PlayerName);
            await BroadcastLobbyState(room);

            if (room.IsScreenPaused)
            {
                await _hubContext.Clients.Client(room.Player1.ConnectionId).SendAsync("GamePausedByScreen");
            }
            else
            {
                StartBotWaitTimer(room);
            }
        }
        else
        {
            await _hubContext.Clients.Group($"Screen_{room.RoomId}").SendAsync("NoActivePlayer");
        }
    }

    // ============================================================
    // E. PENANGANAN BOT (5 DETIK) & TIMEOUT
    // ============================================================
    private void StartBotWaitTimer(RpsRoom room)
    {
        StopBotWaitTimer(room);
        room.BotMatchTimer = new Timer(async _ =>
        {
            bool createBot = false;
            lock (room.Lock)
            {
                if (room.Player1 != null && room.Player2 == null && !room.IsScreenPaused)
                {
                    room.Player2 = new RpsPlayer
                    {
                        ConnectionId = $"BOT_{room.RoomId}",
                        PlayerName = "BOT",
                        IsBot = true
                    };
                    createBot = true;
                }
            }

            if (createBot)
            {
                _logger.LogInformation("[BOT-JOIN] Bot dibuat di Room [{Room}]", room.RoomId);
                await BroadcastLobbyState(room);
                await Task.Delay(1500);
                await StartRound(room);
            }
        }, null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
    }

    private static void StopBotWaitTimer(RpsRoom room)
    {
        if (room.BotMatchTimer != null)
        {
            room.BotMatchTimer.Change(Timeout.Infinite, Timeout.Infinite);
            room.BotMatchTimer.Dispose();
            room.BotMatchTimer = null;
        }
    }

    private void StartRoundTimeoutTimer(RpsRoom room)
    {
        StopRoundTimeoutTimer(room);
        room.RoundTimeoutTimer = new Timer(async _ =>
        {
            string[] opts = { "rock", "paper", "scissors" };
            bool bothAfk = false;

            lock (room.Lock)
            {
                if (room.IsRoundActive && !room.IsScreenPaused)
                {
                    bool p1Afk = room.Player1 != null && string.IsNullOrEmpty(room.Player1.Choice);
                    bool p2Afk = room.Player2 != null && string.IsNullOrEmpty(room.Player2.Choice);

                    if (p1Afk && p2Afk)
                    {
                        bothAfk = true;
                        room.IsRoundActive = false;
                        LogAnalytics(room.RoomId, "MATCH_ABORTED", $"KEDUA pemain AFK di Ronde {room.CurrentRound}. Match dibatalkan otomatis.");
                    }
                    else if (p1Afk)
                    {
                        // P1 AFK: Pilihan akan diacak, biarkan dia tetap main di ronde selanjutnya
                        room.Player1.Choice = opts[Random.Shared.Next(opts.Length)];
                        room.IsRoundActive = false;
                        LogAnalytics(room.RoomId, "PLAYER_AFK", $"Player 1 ({room.Player1.PlayerName}) AFK di Ronde {room.CurrentRound}");
                    }
                    else if (p2Afk)
                    {
                        // P2 AFK: Pilihan akan diacak, biarkan dia tetap main di ronde selanjutnya
                        room.Player2.Choice = opts[Random.Shared.Next(opts.Length)];
                        room.IsRoundActive = false;
                        LogAnalytics(room.RoomId, "PLAYER_AFK", $"Player 2 ({room.Player2.PlayerName}) AFK di Ronde {room.CurrentRound}");
                    }
                }
                else
                {
                    return; // Timer tidak valid
                }
            }

            if (bothAfk)
            {
                // KEDUA PEMAIN AFK -> GAME OVER 
                await _hubContext.Clients.Group($"Screen_{room.RoomId}").SendAsync("BothAfkGameOver");

                if (room.Player1 != null) await _hubContext.Clients.Client(room.Player1.ConnectionId).SendAsync("MatchEnded");
                if (room.Player2 != null) await _hubContext.Clients.Client(room.Player2.ConnectionId).SendAsync("MatchEnded");

                await Task.Delay(4000);
                await EndMatchAndAdvance(room);
            }
            else
            {
                // NORMAL ATAU SALAH SATU AFK -> Lanjut tampilkan siapa yang menang ronde ini
                await CalculateRoundResult(room);
            }
        }, null, TimeSpan.FromSeconds(5), Timeout.InfiniteTimeSpan);
    }

    private static void StopRoundTimeoutTimer(RpsRoom room)
    {
        if (room.RoundTimeoutTimer != null)
        {
            room.RoundTimeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);
            room.RoundTimeoutTimer.Dispose();
            room.RoundTimeoutTimer = null;
        }
    }

    // ============================================================
    // F. BROADCAST & HELPER
    // ============================================================
    private async Task BroadcastLobbyState(RpsRoom room)
    {
        await _hubContext.Clients.Group($"Screen_{room.RoomId}").SendAsync("LobbyState",
            room.Player1?.PlayerName,
            room.Player2?.PlayerName
        );
    }

    private async Task BroadcastQueuePositions(RpsRoom room)
    {
        List<RpsPlayer> snapshot;
        lock (room.Lock)
        {
            snapshot = new List<RpsPlayer>(room.Queue);
        }

        for (int i = 0; i < snapshot.Count; i++)
        {
            await _hubContext.Clients.Client(snapshot[i].ConnectionId).SendAsync("QueueWaiting", i + 1, snapshot[i].PlayerName);
        }
    }

    // ============================================================
    // G. DISCONNECT (VIDEOTRON IKLAN ATAU HP KELUAR)
    // ============================================================
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _screenConnections.TryRemove(Context.ConnectionId, out bool isScreenConn);
        _connectionToRoom.TryRemove(Context.ConnectionId, out string? roomId);

        if (string.IsNullOrEmpty(roomId) || !_rooms.TryGetValue(roomId, out var room))
        {
            await base.OnDisconnectedAsync(exception);
            return;
        }

        bool screenPausedNow = false;
        bool p1Left = false;
        bool p2Left = false;

        lock (room.Lock)
        {
            if (isScreenConn)
            {
                room.ScreenConnectionIds.Remove(Context.ConnectionId);
                // Pause hanya jika SEMUA koneksi layar pada room ini terputus
                if (room.ScreenConnectionIds.Count == 0)
                {
                    room.IsScreenPaused = true;
                    screenPausedNow = true;
                    StopRoundTimeoutTimer(room);
                    StopBotWaitTimer(room);
                }
            }
            else if (room.Player1 != null && room.Player1.ConnectionId == Context.ConnectionId)
            {
                p1Left = true;
                if (room.Player2 == null)
                {
                    // Belum ada musuh sama sekali, hapus saja (jangan jadikan bot)
                    room.Player1 = null;
                }
                else
                {
                    room.Player1.IsBot = true;
                    if (!room.Player1.PlayerName.Contains("BOT")) room.Player1.PlayerName = "BOT";
                }
            }
            else if (room.Player2 != null && room.Player2.ConnectionId == Context.ConnectionId)
            {
                p2Left = true;
                if (room.Player1 == null)
                {
                    room.Player2 = null;
                }
                else
                {
                    room.Player2.IsBot = true;
                    if (!room.Player2.PlayerName.Contains("BOT")) room.Player2.PlayerName = "BOT";
                }
            }
            else
            {
                room.Queue.RemoveAll(p => p.ConnectionId == Context.ConnectionId);
            }
        }

        if (screenPausedNow)
        {
            _logger.LogWarning("[SCREEN-DISCONNECT] Seluruh layar di Room [{Room}] terputus (jeda iklan/tutup). Menjeda HP...", room.RoomId);
            if (room.Player1 != null && !room.Player1.IsBot)
            {
                await _hubContext.Clients.Client(room.Player1.ConnectionId).SendAsync("GamePausedByScreen");
            }
            if (room.Player2 != null && !room.Player2.IsBot)
            {
                await _hubContext.Clients.Client(room.Player2.ConnectionId).SendAsync("GamePausedByScreen");
            }
        }
        else if (p1Left || p2Left)
        {
            bool shouldAbort = false;
            lock (room.Lock)
            {
                bool bothBots = (room.Player1 != null && room.Player1.IsBot) && (room.Player2 != null && room.Player2.IsBot);
                bool bothEmpty = (room.Player1 == null && room.Player2 == null);
                shouldAbort = bothBots || bothEmpty;
            }

            if (shouldAbort)
            {
                _logger.LogWarning("[PLAYER-DISCONNECT] Room kosong atau keduanya BOT. Batal/Lewati match dan panggil antrean...");
                await EndMatchAndAdvance(room);
            }
            else
            {
                _logger.LogWarning("[PLAYER-DISCONNECT] Pemain aktif di Room [{Room}] keluar/refresh. Melanjutkan sebagai BOT.", room.RoomId);
                await BroadcastLobbyState(room);
            }
        }
        else
        {
            await BroadcastQueuePositions(room);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
