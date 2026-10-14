// ============================================================
// DIGITAL PET: VIRTUAL AQUARIUM ENGINE (CANVAS 2D)
// teamLab-inspired interactive DOOH crowd experience
// ============================================================

const urlParams = new URLSearchParams(window.location.search);
const ROOM_ID = (urlParams.get("room") || urlParams.get("site") || "SURABAYA").toUpperCase();
const TITLE = urlParams.get("title") || "VIRTUAL AQUARIUM: DIGITAL PET";

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
// TEMPLATE VEKTOR IKAN & HEWAN LAUT
// ============================================================
const PET_EMOJIS = {
  nemo: "🐠",
  turtle: "🐢",
  octopus: "🐙",
  dolphin: "🐬",
  shark: "🦈",
  puffer: "🐡"
};

// State Akuarium
const activePets = [];
const bubbles = [];
let lastPollTime = Date.now() - 60000; // ambil yang baru
let totalParticipantCount = 0;

// Buat Partikel Gelembung
for (let i = 0; i < 40; i++) {
  bubbles.push({
    x: Math.random() * canvasWidth,
    y: Math.random() * canvasHeight,
    radius: Math.random() * 6 + 2,
    speed: Math.random() * 1.5 + 0.6,
    wobble: Math.random() * Math.PI * 2,
    opacity: Math.random() * 0.5 + 0.2
  });
}

// Ikan Default Awal (Bawaan Aquarium)
const DEFAULT_NAMES = ["Nemo", "Sammy", "Puff", "Dory", "Ollie"];
DEFAULT_NAMES.forEach((name, idx) => {
  const keys = Object.keys(PET_EMOJIS);
  spawnPet({
    id: `bot_${idx}`,
    playerName: name,
    petType: "template",
    templateKey: keys[idx % keys.length],
    colorHex: ["#f97316", "#22c55e", "#a855f7", "#3b82f6", "#eab308"][idx % 5],
    isBot: true
  });
});

// ============================================================
// KELAS PET (IKAN BERENANG)
// ============================================================
function spawnPet(data) {
  const isRight = Math.random() > 0.5;
  const speed = Math.random() * 1.2 + 0.8;
  const size = Math.random() * 20 + 75; // 75px - 95px

  const pet = {
    id: data.id || Math.random().toString(),
    playerName: data.playerName || "Pengunjung",
    petType: data.petType || "template",
    templateKey: data.templateKey || "nemo",
    colorHex: data.colorHex || "#F97316",
    imageData: data.imageData || null,
    imageEl: null,
    isBot: data.isBot || false,

    x: isRight ? -100 : canvasWidth + 100,
    y: Math.random() * (canvasHeight - 250) + 100,
    dx: isRight ? speed : -speed,
    dy: (Math.random() - 0.5) * 0.4,
    size: size,
    angle: 0,
    swimCycle: Math.random() * Math.PI * 2,
    swimSpeed: Math.random() * 0.05 + 0.04
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
// RENDER LOOP (ANIMASI 60 FPS)
// ============================================================
function animate() {
  ctx.clearRect(0, 0, canvasWidth, canvasHeight);

  // 1. Render Gelembung Air
  ctx.save();
  bubbles.forEach((b) => {
    b.y -= b.speed;
    b.wobble += 0.04;
    b.x += Math.sin(b.wobble) * 0.5;

    if (b.y < -20) {
      b.y = canvasHeight + 20;
      b.x = Math.random() * canvasWidth;
    }

    ctx.beginPath();
    ctx.arc(b.x, b.y, b.radius, 0, Math.PI * 2);
    ctx.fillStyle = `rgba(255, 255, 255, ${b.opacity})`;
    ctx.fill();
    ctx.strokeStyle = "rgba(186, 230, 253, 0.6)";
    ctx.lineWidth = 1;
    ctx.stroke();
  });
  ctx.restore();

  // 2. Render Pet Ikan
  activePets.forEach((pet) => {
    // Gerak renang
    pet.x += pet.dx;
    pet.swimCycle += pet.swimSpeed;
    pet.y += Math.sin(pet.swimCycle) * 0.8 + pet.dy;

    // Batas layar atas dan bawah
    if (pet.y < 80) pet.dy = Math.abs(pet.dy);
    if (pet.y > canvasHeight - 160) pet.dy = -Math.abs(pet.dy);

    // Balik arah jika membentur dinding
    if (pet.x > canvasWidth - 40 && pet.dx > 0) {
      pet.dx = -Math.abs(pet.dx);
    } else if (pet.x < 40 && pet.dx < 0) {
      pet.dx = Math.abs(pet.dx);
    }

    ctx.save();
    ctx.translate(pet.x, pet.y);

    // Flip horizontal jika menghadap kiri
    const facingLeft = pet.dx < 0;
    if (facingLeft) {
      ctx.scale(-1, 1);
    }

    // Goyangan sudut ekor
    const tailWiggle = Math.sin(pet.swimCycle * 2) * 0.08;
    ctx.rotate(tailWiggle);

    // Gambar Ikan / Pet
    if (pet.imageEl && pet.imageEl.complete) {
      // Hasil Gambar Coretan Tangan User (Canvas Drawing)
      const drawW = pet.size * 1.3;
      const drawH = pet.size;
      ctx.drawImage(pet.imageEl, -drawW / 2, -drawH / 2, drawW, drawH);
    } else {
      // Template Emoji / Karakter Hewan
      drawTemplateFish(ctx, pet);
    }

    ctx.restore();

    // Gambar Balon Nama Pemain (Selalu tegak tidak ikut berputar)
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

  // Efek bayangan lembut
  context.shadowColor = "rgba(0,0,0,0.4)";
  context.shadowBlur = 10;
  context.fillText(emoji, 0, 0);
  context.shadowBlur = 0;
}

// Gambar Label Nama Pemain di Atas Ikan
function drawNameBadge(context, pet) {
  context.save();
  context.translate(pet.x, pet.y - pet.size * 0.55);

  const text = pet.playerName.toUpperCase();
  context.font = "bold 13px 'Fredoka', 'Poppins', sans-serif";
  const textWidth = context.measureText(text).width;
  const paddingX = 10;
  const badgeWidth = textWidth + paddingX * 2;
  const badgeHeight = 22;

  // Background Badge Kapsul
  context.fillStyle = "rgba(15, 23, 42, 0.85)";
  context.strokeStyle = "#38bdf8";
  context.lineWidth = 1.5;

  context.beginPath();
  context.roundRect(-badgeWidth / 2, -badgeHeight / 2, badgeWidth, badgeHeight, 11);
  context.fill();
  context.stroke();

  // Teks Nama
  context.fillStyle = "#ffffff";
  context.textAlign = "center";
  context.textBaseline = "middle";
  context.fillText(text, 0, 1);

  context.restore();
}

// Update Jumlah Pet di Header
function updateLiveBadge() {
  const countBadge = document.getElementById("activePetCount");
  if (countBadge) {
    countBadge.textContent = `${activePets.length} Pet Berenang`;
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

  // Jika parameter URL memiliki ?host=, utamakan host tersebut
  const customHost = urlParams.get("host");
  if (customHost) {
    baseUrl = customHost.startsWith("http") ? customHost : `http://${customHost}`;
  } else if (window.location.hostname === "localhost" || window.location.hostname === "127.0.0.1") {
    // Jika dibuka di localhost PC, ambil IP LAN server agar bisa di-scan HP
    try {
      const res = await fetch("/api/pet/server-info");
      if (res.ok) {
        const info = await res.json();
        if (info.localIp && info.localIp !== "127.0.0.1") {
          baseUrl = `http://${info.localIp}:5215`;
        }
      }
    } catch (e) {
      console.warn("Could not fetch server-info:", e);
    }
  }

  const controllerUrl = `${baseUrl}/games/digital-pet/controller.html?room=${encodeURIComponent(ROOM_ID)}`;

  qrContainer.innerHTML = "";
  new QRCode(qrContainer, {
    text: controllerUrl,
    width: 140,
    height: 140
  });

  // Tampilkan petunjuk URL di bawah QR
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
