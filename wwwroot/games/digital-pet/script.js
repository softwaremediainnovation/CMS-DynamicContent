// ============================================================
// DIGITAL PET: VIRTUAL AQUARIUM ENGINE (CANVAS 2.5D / PSEUDO-3D)
// teamLab & Dynamic DOOH Interactive Crowd Experience
// — Performance Optimized Edition —
// ============================================================

const urlParams = new URLSearchParams(window.location.search);
const ROOM_ID = (urlParams.get("room") || urlParams.get("site") || "SURABAYA").toUpperCase();
const TITLE = urlParams.get("title") || "AKUARIUM INTERAKTIF";

const canvas = document.getElementById("aquariumCanvas");
const ctx = canvas.getContext("2d");

let canvasWidth = window.innerWidth;
let canvasHeight = window.innerHeight;

// Resize canvas sesuai ukuran layar videotron
function resizeCanvas() {
  canvasWidth = window.innerWidth;
  canvasHeight = window.innerHeight;
  canvas.width = canvasWidth;
  canvas.height = canvasHeight;
}
window.addEventListener("resize", resizeCanvas);
resizeCanvas();

// ============================================================
// TEMPLATE VEKTOR IKAN & HEWAN LAUT (12 KOLEKSI LENGKAP)
// ============================================================
const PET_EMOJIS = {
  nemo: "🐠",
  turtle: "🐢",
  octopus: "🐙",
  dolphin: "🐬",
  shark: "🦈",
  puffer: "🐡",
  whale: "🐋",
  crab: "🦀",
  squid: "🦑",
  dory: "🐟",
  lobster: "🦞",
  seal: "🦭"
};

// ============================================================
// OFFSCREEN EMOJI CACHE (render emoji+shadow SEKALI, pakai terus)
// Menghilangkan shadowBlur per-frame yang sangat berat di GPU
// ============================================================
const emojiCanvasCache = {};

function getEmojiCanvas(templateKey, size) {
  const roundedSize = Math.round(size);
  const cacheKey = `${templateKey}_${roundedSize}`;
  if (emojiCanvasCache[cacheKey]) return emojiCanvasCache[cacheKey];

  const emoji = PET_EMOJIS[templateKey] || "🐠";
  const padding = 16;
  const oCanvas = document.createElement("canvas");
  oCanvas.width = roundedSize + padding * 2;
  oCanvas.height = roundedSize + padding * 2;
  const oCtx = oCanvas.getContext("2d");

  oCtx.font = `${roundedSize}px serif`;
  oCtx.textAlign = "center";
  oCtx.textBaseline = "middle";
  // Shadow di-render SEKALI saat caching — bukan 60x per detik
  oCtx.shadowColor = "rgba(0, 0, 0, 0.45)";
  oCtx.shadowBlur = 12;
  oCtx.fillText(emoji, oCanvas.width / 2, oCanvas.height / 2);

  emojiCanvasCache[cacheKey] = oCanvas;
  return oCanvas;
}

// State Akuarium
const activePets = [];
const bubbles = [];
const sparkles = [];
let petsSortDirty = true; // Flag: sort hanya saat ada perubahan pada array
const BUBBLE_BASE_COUNT = 45;
const BUBBLE_MAX_COUNT = 120; // Batas atas agar burst bubbles tidak menumpuk
let lastPollTime = Date.now() - 60000;
let totalParticipantCount = 0;

// Buat Partikel Gelembung Udara
for (let i = 0; i < BUBBLE_BASE_COUNT; i++) {
  bubbles.push({
    x: Math.random() * canvasWidth,
    y: Math.random() * canvasHeight,
    radius: Math.random() * 6 + 2,
    speed: Math.random() * 1.6 + 0.6,
    wobble: Math.random() * Math.PI * 2,
    opacity: Math.random() * 0.5 + 0.2
  });
}

// Buat Partikel Cahaya Plankton Bawah Air (Underwater Light Dust)
for (let i = 0; i < 30; i++) {
  sparkles.push({
    x: Math.random() * canvasWidth,
    y: Math.random() * canvasHeight,
    radius: Math.random() * 2 + 1,
    alpha: Math.random() * 0.6 + 0.2,
    speedY: (Math.random() - 0.5) * 0.3,
    speedX: (Math.random() - 0.5) * 0.4
  });
}

// Ikan Default Awal (Bawaan Aquarium)
const DEFAULT_NAMES = ["Nemo", "Sammy", "Puff", "Dory", "Ollie", "Willy", "Crabby"];
DEFAULT_NAMES.forEach((name, idx) => {
  const keys = Object.keys(PET_EMOJIS);
  spawnPet({
    id: `bot_${idx}`,
    playerName: name,
    petType: "template",
    templateKey: keys[idx % keys.length],
    colorHex: ["#f97316", "#22c55e", "#a855f7", "#3b82f6", "#eab308", "#ec4899", "#14b8a6"][idx % 7],
    isBot: true
  });
});

// ============================================================
// KELAS PET DENGAN KEDALAMAN Z-AXIS (PSEUDO-3D SWIMMING)
// ============================================================
function spawnPet(data) {
  const isRight = Math.random() > 0.5;
  const depth = data.depth || (Math.random() * 0.45 + 0.65); // 0.65 (jauh) s/d 1.10 (dekat layar)
  const baseSpeed = Math.random() * 1.2 + 0.8;
  const speed = baseSpeed * depth;
  const baseSize = Math.random() * 15 + 75; // 75px - 90px
  const size = baseSize * depth;

  const initialDir = isRight ? 1 : -1;
  const isInitial = data.isInitial || false;

  const pet = {
    id: data.id || Math.random().toString(),
    playerName: data.playerName || "Pengunjung",
    petType: data.petType || "template",
    templateKey: data.templateKey || "nemo",
    colorHex: data.colorHex || "#F97316",
    imageData: data.imageUrl || data.imageData || null,
    imageEl: null,
    isBot: data.isBot || false,

    depth: depth, // Z-Depth untuk efek 3D
    x: isInitial ? (Math.random() * (canvasWidth - 240) + 120) : (isRight ? -120 : canvasWidth + 120),
    y: Math.random() * (canvasHeight - 340) + 120,
    dx: isRight ? speed : -speed,
    dy: (Math.random() - 0.5) * 0.35,
    size: size,
    angle: 0,
    swimCycle: Math.random() * Math.PI * 2,
    swimSpeed: (Math.random() * 0.05 + 0.04) * (1 / depth),

    // 3D Yaw Rotation State (Smooth turn saat berbelok)
    currentScaleX: initialDir,
    targetScaleX: initialDir,

    // Pre-cached name badge measurements (dihitung sekali, bukan 60x/detik)
    _badgeText: (data.playerName || "Pengunjung").toUpperCase(),
    _badgeFontSize: 0,
    _badgeWidth: 0,
    _badgeHeight: 0,
    _badgeDirty: true
  };

  // Preload Image jika ada gambar coretan tangan user (Base64 atau URL file)
  if (pet.imageData) {
    const img = new Image();
    img.crossOrigin = "anonymous";
    img.src = pet.imageData;
    img.onload = () => {
      pet.imageEl = img;
    };
  }

  activePets.push(pet);
  petsSortDirty = true; // Tandai perlu re-sort
  updateLiveBadge();
}

// ============================================================
// RENDER LOOP (ANIMASI 60 FPS DENGAN DEPTH & 3D YAW)
// — Dioptimasi: tanpa shadowBlur live, sort on-demand, bubble cap
// ============================================================
function animate() {
  ctx.clearRect(0, 0, canvasWidth, canvasHeight);

  // 1. Render Partikel Cahaya Plankton Bawah Laut
  // OPTIMASI: Hapus shadowBlur — sebelumnya blur 30 partikel x 60fps = 1800 blur/detik
  sparkles.forEach((s) => {
    s.x += s.speedX;
    s.y += s.speedY;
    if (s.x < 0) s.x = canvasWidth;
    if (s.x > canvasWidth) s.x = 0;
    if (s.y < 0) s.y = canvasHeight;
    if (s.y > canvasHeight) s.y = 0;

    ctx.beginPath();
    ctx.arc(s.x, s.y, s.radius, 0, Math.PI * 2);
    ctx.fillStyle = `rgba(186, 230, 253, ${s.alpha})`;
    ctx.fill();
  });

  // 2. Render Gelembung Udara Alami
  // OPTIMASI: Bersihkan burst bubbles yang sudah keluar layar
  let bi = bubbles.length;
  while (bi-- > BUBBLE_BASE_COUNT) {
    if (bubbles[bi].y < -20) {
      bubbles.splice(bi, 1);
    }
  }

  bubbles.forEach((b) => {
    b.y -= b.speed;
    b.wobble += 0.04;
    b.x += Math.sin(b.wobble) * 0.6;

    // Recycle base bubbles (burst bubbles dibiarkan naik lalu dihapus di cleanup atas)
    if (b.y < -20) {
      b.y = canvasHeight + 20;
      b.x = Math.random() * canvasWidth;
    }

    ctx.beginPath();
    ctx.arc(b.x, b.y, b.radius, 0, Math.PI * 2);
    ctx.fillStyle = `rgba(255, 255, 255, ${b.opacity})`;
    ctx.fill();
    ctx.strokeStyle = "rgba(186, 230, 253, 0.7)";
    ctx.lineWidth = 1;
    ctx.stroke();

    // Kilau kecil di sudut gelembung
    ctx.beginPath();
    ctx.arc(b.x - b.radius * 0.35, b.y - b.radius * 0.35, b.radius * 0.25, 0, Math.PI * 2);
    ctx.fillStyle = "rgba(255, 255, 255, 0.9)";
    ctx.fill();
  });

  // 3. Urutkan Pet Berdasarkan Kedalaman Z (HANYA saat array berubah — bukan tiap frame)
  if (petsSortDirty) {
    activePets.sort((a, b) => a.depth - b.depth);
    petsSortDirty = false;
  }

  // 4. Render Pet Ikan dengan Fisika 3D Yaw & Kedalaman Air
  activePets.forEach((pet) => {
    // Gerak renang organik
    pet.x += pet.dx;
    pet.swimCycle += pet.swimSpeed;
    pet.y += Math.sin(pet.swimCycle) * 0.9 + pet.dy;

    // Batas layar atas dan bawah (jaga agar tidak masuk ke banner atau dasar pasir)
    if (pet.y < 100) pet.dy = Math.abs(pet.dy);
    if (pet.y > canvasHeight - 210) pet.dy = -Math.abs(pet.dy);

    // Balik arah jika membentur dinding dengan 3D Smooth Yaw Rotation
    if (pet.x > canvasWidth - 60 && pet.dx > 0) {
      pet.dx = -Math.abs(pet.dx);
      pet.targetScaleX = -1;
    } else if (pet.x < 60 && pet.dx < 0) {
      pet.dx = Math.abs(pet.dx);
      pet.targetScaleX = 1;
    }

    // Interpolasi halus scaleX (efek ikan berputar 3D horizontal)
    pet.currentScaleX += (pet.targetScaleX - pet.currentScaleX) * 0.085;

    ctx.save();
    ctx.translate(pet.x, pet.y);

    // Efek atmosfer kedalaman air laut (ikan jauh sedikit lebih transparan / biru lembut)
    const depthAlpha = 0.75 + pet.depth * 0.25;
    ctx.globalAlpha = depthAlpha;

    // A. Bayangan Halus di Bawah Ikan (Underwater Depth Shadow)
    ctx.fillStyle = "rgba(2, 28, 52, 0.25)";
    ctx.beginPath();
    ctx.ellipse(0, pet.size * 0.6, pet.size * 0.45 * Math.abs(pet.currentScaleX), pet.size * 0.12, 0, 0, Math.PI * 2);
    ctx.fill();

    // B. Transformasi 3D Yaw (Perspektif Rotasi Horizontal)
    ctx.scale(pet.currentScaleX, 1);

    // Goyangan sudut ekor & sirip (Undulating Swim Wave)
    const turnFactor = 1 - Math.abs(pet.currentScaleX);
    const tailWiggle = Math.sin(pet.swimCycle * 2.2) * (0.08 + turnFactor * 0.12);
    ctx.rotate(tailWiggle);

    // C. Gambar Ikan / Pet
    if (pet.imageEl && pet.imageEl.complete) {
      // Hasil Gambar Coretan Tangan User (Canvas Drawing)
      const drawW = pet.size * 1.35;
      const drawH = pet.size;
      ctx.drawImage(pet.imageEl, -drawW / 2, -drawH / 2, drawW, drawH);
    } else {
      // Template Karakter Hewan Laut — pakai offscreen cached canvas
      drawTemplateFish(ctx, pet);
    }

    ctx.restore();

    // D. Gambar Label Nama Pemain (Selalu tegak tidak ikut berputar)
    drawNameBadge(ctx, pet);
  });

  requestAnimationFrame(animate);
}

// Render Hewan Berdasarkan Template (dari offscreen cache — TANPA shadowBlur live)
function drawTemplateFish(context, pet) {
  const cached = getEmojiCanvas(pet.templateKey, pet.size);
  // Gambar dari offscreen canvas (shadow sudah ter-render di dalamnya)
  context.drawImage(cached, -cached.width / 2, -cached.height / 2);
}

// Gambar Label Nama Pemain di Atas Ikan (dengan cached measurements)
function drawNameBadge(context, pet) {
  const fontSize = Math.max(11, Math.round(13 * pet.depth));
  const fontStr = `bold ${fontSize}px 'Fredoka', 'Poppins', sans-serif`;

  // Hitung badge dimensions SEKALI, cache di pet object
  if (pet._badgeDirty || pet._badgeFontSize !== fontSize) {
    context.font = fontStr;
    const textWidth = context.measureText(pet._badgeText).width;
    const paddingX = 9;
    pet._badgeFontSize = fontSize;
    pet._badgeWidth = textWidth + paddingX * 2;
    pet._badgeHeight = Math.max(18, Math.round(22 * pet.depth));
    pet._badgeDirty = false;
  }

  context.save();
  context.translate(pet.x, pet.y - pet.size * 0.58);

  // Background Badge Kapsul Modern
  context.fillStyle = "rgba(11, 24, 48, 0.88)";
  context.strokeStyle = "#38bdf8";
  context.lineWidth = 1.5;

  context.beginPath();
  context.roundRect(-pet._badgeWidth / 2, -pet._badgeHeight / 2, pet._badgeWidth, pet._badgeHeight, 10);
  context.fill();
  context.stroke();

  // Teks Nama
  context.font = fontStr;
  context.fillStyle = "#ffffff";
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.fillText(pet._badgeText, 0, 1);

  context.restore();
}

// Update Badge Total Live Pet
function updateLiveBadge() {
  const badge = document.getElementById("activePetCount");
  if (badge) {
    badge.textContent = `${activePets.length} Ikan Berenang`;
  }
}

// Tampilkan Toast Saat Ada Audiens Baru Mencemplungkan Pet
function showSpawnToast(name, type) {
  const toast = document.getElementById("spawnToast");
  const text = document.getElementById("spawnToastText");
  if (toast && text) {
    text.textContent = `🎉 ${name.toUpperCase()} baru saja mencemplungkan pet!`;
    toast.classList.add("show");
    setTimeout(() => {
      toast.classList.remove("show");
    }, 4000);
  }
}

// ============================================================
// TAKE DOWN / REMOVE PET DENGAN EFEK BUBBLE BURST
// ============================================================
function removePet(petId) {
  const idx = activePets.findIndex((p) => p.id === petId);
  if (idx !== -1) {
    const pet = activePets[idx];
    // Efek bubble burst saat pet di-take down oleh moderator
    // OPTIMASI: Batasi total bubbles agar tidak membengkak tak terbatas
    const burstCount = Math.min(25, BUBBLE_MAX_COUNT - bubbles.length);
    for (let i = 0; i < burstCount; i++) {
      bubbles.push({
        x: pet.x + (Math.random() - 0.5) * pet.size,
        y: pet.y + (Math.random() - 0.5) * pet.size,
        radius: Math.random() * 8 + 3,
        speed: Math.random() * 3.5 + 1.5,
        wobble: Math.random() * Math.PI * 2,
        opacity: 0.95
      });
    }
    activePets.splice(idx, 1);
    petsSortDirty = true; // Flag re-sort
    updateLiveBadge();
    console.log(`[TakeDown] Pet ${petId} (${pet.playerName}) telah diturunkan dari layar.`);
  }
}

// ============================================================
// LOAD ACTIVE PETS SAAT STARTUP / REFRESH HALAMAN
// ============================================================
async function loadActivePets() {
  try {
    const res = await fetch(`/api/pet/active?room=${encodeURIComponent(ROOM_ID)}&limit=30`);
    if (res.ok) {
      const data = await res.json();
      if (data.pets && data.pets.length > 0) {
        data.pets.forEach((p) => {
          if (!activePets.some((existing) => existing.id === p.id)) {
            p.isInitial = true;
            spawnPet(p);
          }
        });
      }
      if (data.serverTimeUnixMs) {
        lastPollTime = data.serverTimeUnixMs;
      }
    }
  } catch (err) {
    console.warn("Error loading active pets on startup:", err);
  }
}

// ============================================================
// POLLING API KE SERVER (/api/pet/events)
// ============================================================
async function pollNewPets() {
  try {
    const res = await fetch(`/api/pet/events?room=${encodeURIComponent(ROOM_ID)}&sinceUnixMs=${lastPollTime}`);
    if (res.ok) {
      const data = await res.json();

      // Handle Take Down dari server
      if (data.removedPetIds && data.removedPetIds.length > 0) {
        data.removedPetIds.forEach((id) => removePet(id));
      }

      if (data.pets && data.pets.length > 0) {
        data.pets.forEach((p) => {
          // Cegah duplikasi
          if (!activePets.some((existing) => existing.id === p.id)) {
            spawnPet(p);
            showSpawnToast(p.playerName, p.petType);
          }
        });
      }
      if (data.serverTimeUnixMs) {
        lastPollTime = data.serverTimeUnixMs;
      }
    }
  } catch (err) {
    console.warn("Polling pet events error:", err);
  }
}

// Poll setiap 2.5 detik sebagai fallback & sinkronisasi
setInterval(pollNewPets, 2500);

// ============================================================
// SIGNALR REALTIME CONNECTION (INSTANT SPAWN & TAKE DOWN)
// ============================================================
function initSignalR() {
  if (typeof signalR === "undefined") {
    console.warn("SignalR library tidak terdeteksi, mengandalkan polling.");
    return;
  }

  try {
    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/dynamic-content")
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .build();

    connection.on("PetRemoved", (payload) => {
      const petId = typeof payload === "string" ? payload : payload?.petId;
      if (petId) {
        removePet(petId);
      }
    });

    connection.start().then(() => {
      console.log("Connected to SignalR DynamicContentHub.");
      connection.invoke("JoinGroup", `room_${ROOM_ID}`).catch(() => {});
    }).catch((err) => {
      console.warn("SignalR connection failed, fallback to polling:", err);
    });
  } catch (e) {
    console.warn("SignalR init error:", e);
  }
}

// ============================================================
// GENERATE QR CODE HUD
// ============================================================
async function initQrCode() {
  const qrContainer = document.getElementById("qrContainer");
  if (!qrContainer) return;

  let baseUrl = window.location.origin;

  const customHost = urlParams.get("host");
  if (customHost) {
    baseUrl = customHost.startsWith("http") ? customHost : `http://${customHost}`;
  } else if (window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1" || window.location.hostname === "::1") {
    try {
      const res = await fetch("/api/pet/server-info");
      if (res.ok) {
        const info = await res.json();
        if (info.accessibleBaseUrl) {
          baseUrl = info.accessibleBaseUrl;
        } else if (info.localIp && info.localIp !== "127.0.0.1") {
          baseUrl = `http://${info.localIp}:${info.port || 5215}`;
        }
      }
    } catch (e) {
      console.warn("Could not fetch server-info:", e);
    }
  }

  const tokenParam = urlParams.get("token");
  const controllerUrl = tokenParam 
    ? `${baseUrl}/games/digital-pet/controller.html?token=${encodeURIComponent(tokenParam)}`
    : `${baseUrl}/games/digital-pet/controller.html?room=${encodeURIComponent(ROOM_ID)}`;

  qrContainer.innerHTML = "";
  new QRCode(qrContainer, {
    text: controllerUrl,
    width: 150,
    height: 150,
    colorDark: "#000000",
    colorLight: "#ffffff",
    correctLevel: QRCode.CorrectLevel.M,
    quietZone: 8,
    quietZoneColor: "#ffffff"
  });

  const hintEl = document.getElementById("qrUrlHint");
  if (hintEl) {
    hintEl.textContent = controllerUrl;
  }

  const titleEl = document.getElementById("headerTitle");
  if (titleEl) titleEl.textContent = TITLE;
}

window.addEventListener("DOMContentLoaded", () => {
  initQrCode();
  loadActivePets();
  initSignalR();
  animate();
});
