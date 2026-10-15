// ============================================================
// DIGITAL PET: VIRTUAL AQUARIUM ENGINE (CANVAS 2.5D / PSEUDO-3D)
// teamLab & Dynamic DOOH Interactive Crowd Experience
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

// State Akuarium
const activePets = [];
const bubbles = [];
const sparkles = [];
let lastPollTime = Date.now() - 60000;
let totalParticipantCount = 0;

// Buat Partikel Gelembung Udara
for (let i = 0; i < 45; i++) {
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

  const pet = {
    id: data.id || Math.random().toString(),
    playerName: data.playerName || "Pengunjung",
    petType: data.petType || "template",
    templateKey: data.templateKey || "nemo",
    colorHex: data.colorHex || "#F97316",
    imageData: data.imageData || null,
    imageEl: null,
    isBot: data.isBot || false,

    depth: depth, // Z-Depth untuk efek 3D
    x: isRight ? -120 : canvasWidth + 120,
    y: Math.random() * (canvasHeight - 340) + 120,
    dx: isRight ? speed : -speed,
    dy: (Math.random() - 0.5) * 0.35,
    size: size,
    angle: 0,
    swimCycle: Math.random() * Math.PI * 2,
    swimSpeed: (Math.random() * 0.05 + 0.04) * (1 / depth),

    // 3D Yaw Rotation State (Smooth turn saat berbelok)
    currentScaleX: initialDir,
    targetScaleX: initialDir
  };

  // Preload Image jika ada gambar coretan tangan user (Base64)
  if (pet.imageData) {
    const img = new Image();
    img.src = pet.imageData;
    img.onload = () => {
      pet.imageEl = img;
    };
  }

  activePets.push(pet);
  updateLiveBadge();
}

// ============================================================
// RENDER LOOP (ANIMASI 60 FPS DENGAN DEPTH & 3D YAW)
// ============================================================
function animate() {
  ctx.clearRect(0, 0, canvasWidth, canvasHeight);

  // 1. Render Partikel Cahaya Plankton Bawah Laut
  ctx.save();
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
    ctx.shadowColor = "#38bdf8";
    ctx.shadowBlur = 6;
    ctx.fill();
  });
  ctx.restore();

  // 2. Render Gelembung Udara Alami
  ctx.save();
  bubbles.forEach((b) => {
    b.y -= b.speed;
    b.wobble += 0.04;
    b.x += Math.sin(b.wobble) * 0.6;

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
  ctx.restore();

  // 3. Urutkan Pet Berdasarkan Kedalaman Z (Distant Fish di Belakang, Closer Fish di Depan)
  activePets.sort((a, b) => a.depth - b.depth);

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
    ctx.save();
    ctx.fillStyle = "rgba(2, 28, 52, 0.25)";
    ctx.beginPath();
    ctx.ellipse(0, pet.size * 0.6, pet.size * 0.45 * Math.abs(pet.currentScaleX), pet.size * 0.12, 0, 0, Math.PI * 2);
    ctx.fill();
    ctx.restore();

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

      // Glow lembut bawah air untuk coretan tangan
      ctx.shadowColor = "rgba(56, 189, 248, 0.4)";
      ctx.shadowBlur = 12;
      ctx.drawImage(pet.imageEl, -drawW / 2, -drawH / 2, drawW, drawH);
      ctx.shadowBlur = 0;
    } else {
      // Template Karakter Hewan Laut
      drawTemplateFish(ctx, pet);
    }

    ctx.restore();

    // D. Gambar Label Nama Pemain (Selalu tegak tidak ikut berputar)
    drawNameBadge(ctx, pet);
  });

  requestAnimationFrame(animate);
}

// Render Hewan Berdasarkan Template
function drawTemplateFish(context, pet) {
  const emoji = PET_EMOJIS[pet.templateKey] || "🐠";
  context.font = `${pet.size}px serif`;
  context.textAlign = "center";
  context.textBaseline = "middle";

  // Efek bayangan & kilau cahaya 3D bawah air
  context.shadowColor = "rgba(0, 0, 0, 0.45)";
  context.shadowBlur = 12;
  context.fillText(emoji, 0, 0);
  context.shadowBlur = 0;
}

// Gambar Label Nama Pemain di Atas Ikan
function drawNameBadge(context, pet) {
  context.save();
  context.translate(pet.x, pet.y - pet.size * 0.58);

  const text = pet.playerName.toUpperCase();
  context.font = `bold ${Math.max(11, Math.round(13 * pet.depth))}px 'Fredoka', 'Poppins', sans-serif`;
  const textWidth = context.measureText(text).width;
  const paddingX = 9;
  const badgeWidth = textWidth + paddingX * 2;
  const badgeHeight = Math.max(18, Math.round(22 * pet.depth));

  // Background Badge Kapsul Modern
  context.fillStyle = "rgba(11, 24, 48, 0.88)";
  context.strokeStyle = "#38bdf8";
  context.lineWidth = 1.5;

  context.beginPath();
  context.roundRect(-badgeWidth / 2, -badgeHeight / 2, badgeWidth, badgeHeight, 10);
  context.fill();
  context.stroke();

  // Teks Nama
  context.fillStyle = "#ffffff";
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.fillText(text, 0, 1);

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
// POLLING API KE SERVER (/api/pet/events)
// ============================================================
async function pollNewPets() {
  try {
    const res = await fetch(`/api/pet/events?room=${encodeURIComponent(ROOM_ID)}&sinceUnixMs=${lastPollTime}`);
    if (res.ok) {
      const data = await res.json();
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

// Poll setiap 2.5 detik
setInterval(pollNewPets, 2500);

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
  animate();
});
