using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;

namespace CmsBillboard.DynamicContent.Controllers;

[ApiController]
[Route("api/pet")]
public class PetApiController : ControllerBase
{
    // In-memory queue pet per screen/room
    private static readonly ConcurrentDictionary<string, ConcurrentBag<PetSpawnItem>> RoomPets = new();
    private static readonly ConcurrentDictionary<string, int> TotalSpawnsPerRoom = new();

    [HttpGet("server-info")]
    public IActionResult GetServerInfo()
    {
        string? localIp = null;
        try
        {
            localIp = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                            && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                                    && !System.Net.IPAddress.IsLoopback(a.Address))?.Address.ToString();
        }
        catch { }

        var port = HttpContext.Request.Host.Port ?? 5215;
        var scheme = HttpContext.Request.Scheme;

        return Ok(new
        {
            localIp = localIp ?? "127.0.0.1",
            port = port,
            httpBaseUrl = $"http://{localIp ?? "localhost"}:{port}",
            currentBaseUrl = $"{scheme}://{HttpContext.Request.Host}"
        });
    }

    [HttpPost("spawn")]
    public IActionResult SpawnPet([FromBody] PetSpawnRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PlayerName))
        {
            return BadRequest(new { success = false, message = "Nama pemain wajib diisi." });
        }

        var room = string.IsNullOrWhiteSpace(request.Room) ? "DEFAULT" : request.Room.Trim().ToUpper();
        
        var petItem = new PetSpawnItem
        {
            Id = Guid.NewGuid().ToString("N")[..8],
            Room = room,
            PlayerName = request.PlayerName.Trim().Length > 20 ? request.PlayerName.Trim()[..20] : request.PlayerName.Trim(),
            PetType = string.IsNullOrWhiteSpace(request.PetType) ? "template" : request.PetType,
            TemplateKey = request.TemplateKey ?? "nemo",
            ImageData = request.ImageData,
            ColorHex = request.ColorHex ?? "#F97316",
            CreatedAt = DateTime.UtcNow
        };

        var bag = RoomPets.GetOrAdd(room, _ => new ConcurrentBag<PetSpawnItem>());
        bag.Add(petItem);

        TotalSpawnsPerRoom.AddOrUpdate(room, 1, (_, current) => current + 1);

        return Ok(new
        {
            success = true,
            message = "Pet berhasil dicemplungkan ke akuarium!",
            pet = petItem
        });
    }

    [HttpGet("events")]
    public IActionResult GetPendingPets([FromQuery] string? room, [FromQuery] long sinceUnixMs = 0)
    {
        var targetRoom = string.IsNullOrWhiteSpace(room) ? "DEFAULT" : room.Trim().ToUpper();
        var sinceTime = DateTimeOffset.FromUnixTimeMilliseconds(sinceUnixMs).UtcDateTime;

        if (!RoomPets.TryGetValue(targetRoom, out var bag))
        {
            return Ok(new
            {
                room = targetRoom,
                totalSpawns = TotalSpawnsPerRoom.GetValueOrDefault(targetRoom, 0),
                pets = Array.Empty<PetSpawnItem>(),
                serverTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }

        // Ambil pet yang dibuat setelah sinceTime
        var newPets = bag
            .Where(p => p.CreatedAt > sinceTime)
            .OrderBy(p => p.CreatedAt)
            .Take(30)
            .ToList();

        return Ok(new
        {
            room = targetRoom,
            totalSpawns = TotalSpawnsPerRoom.GetValueOrDefault(targetRoom, 0),
            pets = newPets,
            serverTimeUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
    }

    [HttpGet("stats")]
    public IActionResult GetStats([FromQuery] string? room)
    {
        var targetRoom = string.IsNullOrWhiteSpace(room) ? "DEFAULT" : room.Trim().ToUpper();
        var total = TotalSpawnsPerRoom.GetValueOrDefault(targetRoom, 0);
        return Ok(new { room = targetRoom, totalPets = total });
    }
}

public class PetSpawnRequest
{
    public string? Room { get; set; } = "DEFAULT";
    public string PlayerName { get; set; } = string.Empty;
    public string? PetType { get; set; } = "template"; // "template" or "draw"
    public string? TemplateKey { get; set; } = "nemo"; // nemo, turtle, octopus, dolphin, shark, puffer
    public string? ImageData { get; set; } // Base64 data URL jika draw
    public string? ColorHex { get; set; } = "#F97316";
}

public class PetSpawnItem
{
    public string Id { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;
    public string PetType { get; set; } = "template";
    public string TemplateKey { get; set; } = "nemo";
    public string? ImageData { get; set; }
    public string ColorHex { get; set; } = "#F97316";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
