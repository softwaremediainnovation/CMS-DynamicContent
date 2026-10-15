// ============================================================
// SUIT: BATU GUNTING KERTAS - DOOH SCREEN CLIENT
// Mendukung Controller HP via QR Code + SignalR Hub (/hubs/rps)
// Dilengkapi fallback Touchscreen / Demo lokal di layar
// ============================================================

const urlParams = new URLSearchParams(window.location.search);

// 1. Tentukan Room / Site ID
function getRoomId() {
  const paramRoom = urlParams.get("room") || urlParams.get("site");
  if (paramRoom && paramRoom.trim()) {
    const cleanRoom = paramRoom.trim().toUpperCase();
    localStorage.setItem("rps_screen_id", cleanRoom);
    return cleanRoom;
  }

  let stored = localStorage.getItem("rps_screen_id");
  if (!stored) {
    stored = "SURABAYA";
    localStorage.setItem("rps_screen_id", stored);
  }
  return stored;
}

const currentRoomId = getRoomId();
const BACKEND_URL = urlParams.get("backend") || window.location.origin;

// 2. Custom Campaign Title dari query / CMS params
const campaignTitleParam = urlParams.get("title");
if (campaignTitleParam) {
  const titleEl = document.getElementById("banner-campaign-title");
  if (titleEl) titleEl.textContent = campaignTitleParam;
}

// 3. DOM Containers & Elements
const lobbyContainer = document.getElementById("lobby-container");
const battleContainer = document.getElementById("battle-container");
const interactiveActionContainer = document.getElementById("interactive-action-container");

// Player 1 Elements
const p1AvatarImg = document.getElementById("p1-avatar-img");
const p1NameEl = document.getElementById("p1-name");
const p1ScoreVal = document.getElementById("p1-score-val");

// Player 2 Elements
const p2AvatarImg = document.getElementById("p2-avatar-img");
const p2NameEl = document.getElementById("p2-name");
const p2ScoreVal = document.getElementById("p2-score-val");

// Hands Elements
const p1HandImg = document.getElementById("p1-hand-img");
const p1LabelPill = document.getElementById("p1-label-pill");
const p1ChoiceText = document.getElementById("p1-choice-text");

const p2HandImg = document.getElementById("p2-hand-img");
const p2LabelPill = document.getElementById("p2-label-pill");
const p2ChoiceText = document.getElementById("p2-choice-text");

// Timer & Badges
const countdownTimerBox = document.getElementById("countdown-timer-box");
const roundTimerEl = document.getElementById("round-timer");

// Winner Panel
const winnerDisplayPanel = document.getElementById("winner-display-panel");
const crownIconBox = document.getElementById("crown-icon-box");
const winnerNameText = document.getElementById("winner-name-text");
const winnerStatusText = document.getElementById("winner-status-text");

// Lobby Elements
const slotP1 = document.getElementById("slot-p1");
const slotP2 = document.getElementById("slot-p2");
const screenIdBadge = document.getElementById("screen-id-badge");
const serverStatusBadge = document.getElementById("server-status-badge");

// Mapping Pilihan Tangan
const HAND_MAP = {
  rock: { label: "BATU", img: "./images/hand-rock.svg" },
  paper: { label: "KERTAS", img: "./images/hand-paper.svg" },
  scissors: { label: "GUNTING", img: "./images/hand-scissors.svg" }
};

let countdownInterval = null;
let isDemoTouchMode = false;
let localP1Score = 0;
let localP2Score = 0;

// Avatar Generator
function getAvatarUrl(name) {
  if (!name) return "https://api.dicebear.com/7.x/bottts/svg?seed=BotMaster";
  const cleanName = name.trim().toUpperCase();
  if (cleanName.includes("BOT") || cleanName.includes("KOMPUTER") || cleanName.includes("AI")) {
    return `https://api.dicebear.com/7.x/bottts/svg?seed=${encodeURIComponent(name)}`;
  }
  return `https://api.dicebear.com/7.x/avataaars/svg?seed=${encodeURIComponent(name)}`;
}

function updateScreenBadges() {
  if (screenIdBadge) {
    screenIdBadge.textContent = `Layar: ${currentRoomId}`;
  }
}

function updateServerStatus(text, color) {
  if (serverStatusBadge) {
    serverStatusBadge.textContent = text;
    serverStatusBadge.style.color = color;
    serverStatusBadge.style.borderColor = color;
  }
}

// Visual Timer Countdown
function startVisualTimer(duration = 5) {
  if (!roundTimerEl) return;
  let timeLeft = duration;
  roundTimerEl.innerText = timeLeft;
  roundTimerEl.style.color = "#ffffff";

  clearInterval(countdownInterval);
  countdownInterval = setInterval(() => {
    timeLeft--;
    if (timeLeft <= 2) {
      roundTimerEl.style.color = "#ef4444";
    }
    if (timeLeft <= 0) {
      timeLeft = 0;
      clearInterval(countdownInterval);
    }
    roundTimerEl.innerText = timeLeft;
  }, 1000);
}

function stopVisualTimer() {
  clearInterval(countdownInterval);
}

// Mode: "lobby" vs "battle"
function setMode(mode) {
  if (mode === "lobby") {
    stopVisualTimer();
    if (battleContainer) battleContainer.style.display = "none";
    if (lobbyContainer) lobbyContainer.style.display = "flex";
  } else if (mode === "battle") {
    if (lobbyContainer) lobbyContainer.style.display = "none";
    if (battleContainer) battleContainer.style.display = "flex";
  }
}

// Reset Pose Tangan saat ronde dimulai
function resetDuelHandsForRound() {
  if (p1HandImg) {
    p1HandImg.src = "./images/hand-rock.svg";
    p1HandImg.className = "hand-svg-graphic shaking-left";
  }
  if (p1LabelPill) p1LabelPill.style.display = "none";

  if (p2HandImg) {
    p2HandImg.src = "./images/hand-rock.svg";
    p2HandImg.className = "hand-svg-graphic flipped-hand shaking-right";
  }
  if (p2LabelPill) p2LabelPill.style.display = "none";

  if (winnerDisplayPanel) winnerDisplayPanel.style.display = "none";
  if (countdownTimerBox) countdownTimerBox.style.display = "flex";
}

// ============================================================
// 4. GENERATE QR CODE (AUTO LAN IP RESOLUTION)
// ============================================================
async function initQrCode() {
  updateScreenBadges();
  const qrContainer = document.getElementById("qrcode");
  if (!qrContainer) return;
  qrContainer.innerHTML = "";

  let baseHttpUrl = window.location.origin;

  // Hanya jika dibuka di localhost/127.0.0.1, minta server resolve IP LAN/Tunnel publik
  const isLoopback = window.location.hostname === "localhost" || 
                     window.location.hostname === "127.0.0.1" || 
                     window.location.hostname === "::1";

  if (isLoopback) {
    try {
      const res = await fetch("/api/pet/server-info");
      if (res.ok) {
        const data = await res.json();
        if (data.accessibleBaseUrl) {
          baseHttpUrl = data.accessibleBaseUrl;
        } else if (data.httpBaseUrl) {
          baseHttpUrl = data.httpBaseUrl;
        }
      }
    } catch (err) {
      console.warn("Gagal auto-resolve IP/Tunnel, fallback ke origin:", err);
    }
  }

  // Bangun URL controller HP (gunakan token terenkripsi jika ada, fallback ke room)
  const tokenParam = urlParams.get("token");
  const controllerUrl = tokenParam 
    ? `${baseHttpUrl}/games/rock-paper-scissors/controller.html?token=${encodeURIComponent(tokenParam)}`
    : `${baseHttpUrl}/games/rock-paper-scissors/controller.html?room=${encodeURIComponent(currentRoomId)}`;

  console.log("RPS Controller Mobile URL:", controllerUrl);

  new QRCode(qrContainer, {
    text: controllerUrl,
    width: 220,
    height: 220,
    colorDark: "#000000",
    colorLight: "#ffffff",
    correctLevel: QRCode.CorrectLevel.M,
    quietZone: 10,
    quietZoneColor: "#ffffff"
  });
}

// ============================================================
// 5. SIGNALR HUB CONNECTION
// ============================================================
const connection = new signalR.HubConnectionBuilder()
  .withUrl(BACKEND_URL + "/hubs/rps", {
    skipNegotiation: false,
    transport: signalR.HttpTransportType.WebSockets | signalR.HttpTransportType.LongPolling
  })
  .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
  .configureLogging(signalR.LogLevel.Information)
  .build();

// Event: Update status lobi antrean pemain
connection.on("LobbyState", (p1Name, p2Name) => {
  if (isDemoTouchMode) return;
  setMode("lobby");

  if (p1Name) {
    if (slotP1) {
      slotP1.textContent = `Player 1: ${p1Name}`;
      slotP1.style.borderColor = "#4ade80";
      slotP1.style.color = "#4ade80";
    }
    if (p1NameEl) p1NameEl.textContent = p1Name;
    if (p1AvatarImg) p1AvatarImg.src = getAvatarUrl(p1Name);
  } else {
    if (slotP1) {
      slotP1.textContent = "Player 1: Menunggu...";
      slotP1.style.borderColor = "";
      slotP1.style.color = "";
    }
    if (p1NameEl) p1NameEl.textContent = "MENUNGGU";
    if (p1AvatarImg) p1AvatarImg.src = "https://api.dicebear.com/7.x/avataaars/svg?seed=Andy";
  }

  if (p2Name) {
    if (slotP2) {
      slotP2.textContent = `Player 2: ${p2Name}`;
      slotP2.style.borderColor = "#4ade80";
      slotP2.style.color = "#4ade80";
    }
    if (p2NameEl) p2NameEl.textContent = p2Name;
    if (p2AvatarImg) p2AvatarImg.src = getAvatarUrl(p2Name);
  } else if (p1Name) {
    if (slotP2) {
      slotP2.textContent = "Mencari lawan...";
      slotP2.style.borderColor = "#facc15";
      slotP2.style.color = "#facc15";
    }
    if (p2NameEl) p2NameEl.textContent = "MENUNGGU";
    if (p2AvatarImg) p2AvatarImg.src = "https://api.dicebear.com/7.x/bottts/svg?seed=BotMaster";
  } else {
    if (slotP2) {
      slotP2.textContent = "Player 2: Menunggu...";
      slotP2.style.borderColor = "";
      slotP2.style.color = "";
    }
    if (p2NameEl) p2NameEl.textContent = "MENUNGGU";
    if (p2AvatarImg) p2AvatarImg.src = "https://api.dicebear.com/7.x/bottts/svg?seed=BotMaster";
  }
});

// Event: Tidak ada pemain aktif -> Kembali ke Standby QR
connection.on("NoActivePlayer", () => {
  if (isDemoTouchMode) return;
  setMode("lobby");
  if (slotP1) {
    slotP1.textContent = "Player 1: Menunggu...";
    slotP1.style.borderColor = "";
    slotP1.style.color = "";
  }
  if (slotP2) {
    slotP2.textContent = "Player 2: Menunggu...";
    slotP2.style.borderColor = "";
    slotP2.style.color = "";
  }
  if (p1NameEl) p1NameEl.textContent = "MENUNGGU";
  if (p2NameEl) p2NameEl.textContent = "MENUNGGU";
  if (p1ScoreVal) p1ScoreVal.textContent = "0";
  if (p2ScoreVal) p2ScoreVal.textContent = "0";
});

// Event: Ronde Dimulai dari Server
connection.on("RoundStarted", (p1Name, p1Score, p2Name, p2Score, round) => {
  isDemoTouchMode = false;
  if (interactiveActionContainer) interactiveActionContainer.style.display = "none";

  setMode("battle");

  if (p1NameEl) p1NameEl.textContent = p1Name;
  if (p2NameEl) p2NameEl.textContent = p2Name;
  if (p1ScoreVal) p1ScoreVal.textContent = p1Score;
  if (p2ScoreVal) p2ScoreVal.textContent = p2Score;

  if (p1AvatarImg) p1AvatarImg.src = getAvatarUrl(p1Name);
  if (p2AvatarImg) p2AvatarImg.src = getAvatarUrl(p2Name);

  resetDuelHandsForRound();
  startVisualTimer(5);
});

// Event: Menerima Hasil Ronde dari Server
connection.on("RoundResult", (p1Name, c1, p1Score, p2Name, c2, p2Score, winner, isMatchOver, matchWinner) => {
  stopVisualTimer();
  setMode("battle");

  if (p1ScoreVal) p1ScoreVal.textContent = p1Score;
  if (p2ScoreVal) p2ScoreVal.textContent = p2Score;

  // Render Pilihan Tangan Player 1 (Kiri)
  const choice1 = HAND_MAP[c1] || { label: (c1 || "BATU").toUpperCase(), img: "./images/hand-rock.svg" };
  if (p1HandImg) {
    p1HandImg.src = choice1.img;
    p1HandImg.className = "hand-svg-graphic clash-in-left";
  }
  if (p1ChoiceText) p1ChoiceText.textContent = choice1.label;
  if (p1LabelPill) p1LabelPill.style.display = "inline-flex";

  // Render Pilihan Tangan Player 2 (Kanan)
  const choice2 = HAND_MAP[c2] || { label: (c2 || "GUNTING").toUpperCase(), img: "./images/hand-scissors.svg" };
  if (p2HandImg) {
    p2HandImg.src = choice2.img;
    p2HandImg.className = "hand-svg-graphic flipped-hand clash-in-right";
  }
  if (p2ChoiceText) p2ChoiceText.textContent = choice2.label;
  if (p2LabelPill) p2LabelPill.style.display = "inline-flex";

  if (countdownTimerBox) countdownTimerBox.style.display = "none";

  // Tampilkan Pengumuman Pemenang
  setTimeout(() => {
    if (winnerDisplayPanel) {
      winnerDisplayPanel.style.display = "flex";

      if (isMatchOver) {
        if (crownIconBox) crownIconBox.style.display = "block";
        if (winnerNameText) winnerNameText.textContent = matchWinner || (p1Score > p2Score ? p1Name : p2Name);
        if (winnerStatusText) winnerStatusText.textContent = "JUARA MATCH!";
      } else {
        if (winner === "p1") {
          if (crownIconBox) crownIconBox.style.display = "block";
          if (winnerNameText) winnerNameText.textContent = p1Name;
          if (winnerStatusText) winnerStatusText.textContent = "MENANG RONDE!";
        } else if (winner === "p2") {
          if (crownIconBox) crownIconBox.style.display = "block";
          if (winnerNameText) winnerNameText.textContent = p2Name;
          if (winnerStatusText) winnerStatusText.textContent = "MENANG RONDE!";
        } else {
          if (crownIconBox) crownIconBox.style.display = "none";
          if (winnerNameText) winnerNameText.textContent = "RONDE";
          if (winnerStatusText) winnerStatusText.textContent = "HASIL SERI!";
        }
      }
    }
  }, 300);
});

// Event: Kedua Pemain AFK
connection.on("BothAfkGameOver", () => {
  stopVisualTimer();
  setMode("battle");

  if (p1LabelPill) p1LabelPill.style.display = "none";
  if (p2LabelPill) p2LabelPill.style.display = "none";
  if (countdownTimerBox) countdownTimerBox.style.display = "none";

  if (winnerDisplayPanel) {
    winnerDisplayPanel.style.display = "flex";
    if (crownIconBox) crownIconBox.style.display = "none";
    if (winnerNameText) winnerNameText.textContent = "PEMAIN AFK";
    if (winnerStatusText) winnerStatusText.textContent = "GAME OVER";
  }
});

// Resume State
function applyMatchState(p1Name, p1Score, p2Name, p2Score, round) {
  stopVisualTimer();
  setMode("battle");

  if (p1NameEl) p1NameEl.textContent = p1Name;
  if (p2NameEl) p2NameEl.textContent = p2Name;
  if (p1ScoreVal) p1ScoreVal.textContent = p1Score;
  if (p2ScoreVal) p2ScoreVal.textContent = p2Score;

  if (p1AvatarImg) p1AvatarImg.src = getAvatarUrl(p1Name);
  if (p2AvatarImg) p2AvatarImg.src = getAvatarUrl(p2Name);

  resetDuelHandsForRound();
  startVisualTimer(5);
}

connection.on("GameResumeForScreen", applyMatchState);
connection.on("MatchInProgress", applyMatchState);

// Lifecycle SignalR
connection.onreconnecting((err) => {
  console.warn("SignalR Reconnecting:", err);
  updateServerStatus("Reconnecting...", "#facc15");
});

connection.onreconnected(async (connectionId) => {
  console.log("Reconnected to SignalR:", connectionId);
  updateServerStatus("Terhubung", "#4ade80");
  try {
    await connection.invoke("RegisterScreen", currentRoomId);
  } catch (err) {
    console.error("Gagal mendaftar ulang layar:", err);
  }
});

connection.onclose((err) => {
  console.warn("SignalR Connection Closed:", err);
  updateServerStatus("Terputus", "#f87171");
  setTimeout(startSignalR, 4000);
});

async function startSignalR() {
  updateServerStatus("Menghubungkan...", "#94a3b8");
  try {
    await connection.start();
    console.log(`Terhubung ke RPS Hub! Layar Room: ${currentRoomId}`);
    updateServerStatus("Online", "#4ade80");
    await connection.invoke("RegisterScreen", currentRoomId);
  } catch (err) {
    console.warn("Gagal connect SignalR (akan dicoba lagi):", err);
    updateServerStatus("Offline (Demo Mode Siap)", "#facc15");
    setTimeout(startSignalR, 5000);
  }
}

// ============================================================
// 6. MODE FALLBACK TOUCHSCREEN / DEMO LOKAL DI LAYAR
// ============================================================
function enableDemoTouchMode() {
  isDemoTouchMode = true;
  setMode("battle");

  if (p1NameEl) p1NameEl.textContent = "PEMAIN (TOUCH)";
  if (p2NameEl) p2NameEl.textContent = "BOT RETRO";
  if (p1ScoreVal) p1ScoreVal.textContent = localP1Score;
  if (p2ScoreVal) p2ScoreVal.textContent = localP2Score;

  if (interactiveActionContainer) interactiveActionContainer.style.display = "block";
  resetDuelHandsForRound();
  startVisualTimer(5);
}

function backToQrLobby() {
  isDemoTouchMode = false;
  if (interactiveActionContainer) interactiveActionContainer.style.display = "none";
  setMode("lobby");
}

let isLocalRoundBusy = false;
function playLocalRound(playerChoice) {
  if (isLocalRoundBusy) return;
  isLocalRoundBusy = true;
  stopVisualTimer();

  const choices = ["rock", "paper", "scissors"];
  const botChoice = choices[Math.floor(Math.random() * choices.length)];

  resetDuelHandsForRound();

  // Suspense shaking 1.5 detik
  setTimeout(() => {
    const choice1 = HAND_MAP[playerChoice];
    if (p1HandImg) {
      p1HandImg.src = choice1.img;
      p1HandImg.className = "hand-svg-graphic clash-in-left";
    }
    if (p1ChoiceText) p1ChoiceText.textContent = choice1.label;
    if (p1LabelPill) p1LabelPill.style.display = "inline-flex";

    const choice2 = HAND_MAP[botChoice];
    if (p2HandImg) {
      p2HandImg.src = choice2.img;
      p2HandImg.className = "hand-svg-graphic flipped-hand clash-in-right";
    }
    if (p2ChoiceText) p2ChoiceText.textContent = choice2.label;
    if (p2LabelPill) p2LabelPill.style.display = "inline-flex";

    if (countdownTimerBox) countdownTimerBox.style.display = "none";

    let winner = "draw";
    if (playerChoice === botChoice) {
      winner = "draw";
    } else if (
      (playerChoice === "rock" && botChoice === "scissors") ||
      (playerChoice === "paper" && botChoice === "rock") ||
      (playerChoice === "scissors" && botChoice === "paper")
    ) {
      winner = "p1";
      localP1Score++;
    } else {
      winner = "p2";
      localP2Score++;
    }

    if (p1ScoreVal) p1ScoreVal.textContent = localP1Score;
    if (p2ScoreVal) p2ScoreVal.textContent = localP2Score;

    setTimeout(() => {
      if (winnerDisplayPanel) {
        winnerDisplayPanel.style.display = "flex";
        if (winner === "p1") {
          if (crownIconBox) crownIconBox.style.display = "block";
          if (winnerNameText) winnerNameText.textContent = "PEMAIN";
          if (winnerStatusText) winnerStatusText.textContent = "MENANG!";
        } else if (winner === "p2") {
          if (crownIconBox) crownIconBox.style.display = "block";
          if (winnerNameText) winnerNameText.textContent = "BOT RETRO";
          if (winnerStatusText) winnerStatusText.textContent = "MENANG!";
        } else {
          if (crownIconBox) crownIconBox.style.display = "none";
          if (winnerNameText) winnerNameText.textContent = "HASIL";
          if (winnerStatusText) winnerStatusText.textContent = "SERI!";
        }
      }

      // Selesai ronde, siap ronde berikutnya
      setTimeout(() => {
        isLocalRoundBusy = false;
        if (isDemoTouchMode) {
          resetDuelHandsForRound();
          startVisualTimer(5);
        }
      }, 3000);
    }, 400);
  }, 1200);
}

// Inisialisasi awal saat load
window.addEventListener("DOMContentLoaded", () => {
  initQrCode();
  startSignalR();
});
