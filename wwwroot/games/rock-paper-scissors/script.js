// ============================================================
// ROCK-PAPER-SCISSORS (CLIENT-SIDE STANDALONE ENGINE)
// Dinamis, Responsive & Plug-and-Play untuk CMS Videotron
// ============================================================

// Pilihan Tangan & Asset
const HAND_MAP = {
  rock: {
    label: "BATU",
    img: "./images/hand-rock.svg"
  },
  paper: {
    label: "KERTAS",
    img: "./images/hand-paper.svg"
  },
  scissors: {
    label: "GUNTING",
    img: "./images/hand-scissors.svg"
  }
};

const CHOICES = ["rock", "scissors", "paper"];

// Query Parameters untuk Konfigurasi Dinamis dari CMS
const urlParams = new URLSearchParams(window.location.search);
const paramTitle = urlParams.get("title") || "SUIT: BATU, GUNTING, KERTAS";
const paramP1Name = urlParams.get("p1") || "PEMAIN";
const paramP2Name = urlParams.get("p2") || "KOMPUTER";
const initialModeAuto = urlParams.get("mode") === "auto";

// State Permainan
let p1Score = 0;
let p2Score = 0;
let roundCount = 0;
let isPlayingRound = false;
let isAutoMode = initialModeAuto;
let autoPlayTimer = null;
let countdownInterval = null;

// DOM Elements
const bannerTitleEl = document.getElementById("banner-campaign-title");
const p1NameEl = document.getElementById("p1-name");
const p2NameEl = document.getElementById("p2-name");
const p1ScoreEl = document.getElementById("p1-score-val");
const p2ScoreEl = document.getElementById("p2-score-val");

const p1HandImg = document.getElementById("p1-hand-img");
const p2HandImg = document.getElementById("p2-hand-img");
const p1LabelPill = document.getElementById("p1-label-pill");
const p2LabelPill = document.getElementById("p2-label-pill");
const p1ChoiceText = document.getElementById("p1-choice-text");
const p2ChoiceText = document.getElementById("p2-choice-text");

const countdownTimerBox = document.getElementById("countdown-timer-box");
const roundTimerEl = document.getElementById("round-timer");

const winnerDisplayPanel = document.getElementById("winner-display-panel");
const crownIconBox = document.getElementById("crown-icon-box");
const winnerNameText = document.getElementById("winner-name-text");
const winnerStatusText = document.getElementById("winner-status-text");

const actionPromptText = document.getElementById("action-prompt-text");
const btnToggleMode = document.getElementById("btn-toggle-mode");

// Inisialisasi Tampilan Awal
function initGame() {
  if (bannerTitleEl) bannerTitleEl.textContent = paramTitle;
  if (p1NameEl) p1NameEl.textContent = paramP1Name.toUpperCase();
  if (p2NameEl) p2NameEl.textContent = paramP2Name.toUpperCase();

  updateModeUI();
  resetDuelHands();

  if (isAutoMode) {
    scheduleNextAutoRound(2000);
  }
}

// Update Tombol & Label Mode
function updateModeUI() {
  if (!btnToggleMode) return;
  if (isAutoMode) {
    btnToggleMode.textContent = "Mode: Auto Demo";
    btnToggleMode.classList.add("active");
    if (actionPromptText) actionPromptText.textContent = "MODE AUTO-PLAY (SENTUH TOMBOL UNTUK MAIN SENDIRI)";
  } else {
    btnToggleMode.textContent = "Mode: Interaktif";
    btnToggleMode.classList.remove("active");
    if (actionPromptText) actionPromptText.textContent = "PILIH TANGANMU UNTUK BERTANDING!";
  }
}

// Toggle Mode Manual / Auto
function toggleAutoMode() {
  isAutoMode = !isAutoMode;
  updateModeUI();

  if (isAutoMode) {
    scheduleNextAutoRound(1500);
  } else {
    clearTimeout(autoPlayTimer);
  }
}

// Reset Posisi Tangan ke Animasi Kepalan Goyang (Shaking Hands)
function resetDuelHands() {
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

// Eksekusi Ronde (Batu, Gunting, Kertas)
function playRound(playerChoice) {
  if (isPlayingRound) return;
  isPlayingRound = true;

  // Jika user mengklik tombol di saat mode auto, matikan auto sementara
  clearTimeout(autoPlayTimer);

  roundCount++;
  if (roundTimerEl) roundTimerEl.textContent = roundCount;

  // Reset visual ke fase shaking
  resetDuelHands();
  if (actionPromptText) actionPromptText.textContent = "BERTANDING...";

  // Pilihan acak untuk Komputer (AI)
  const computerChoice = CHOICES[Math.floor(Math.random() * CHOICES.length)];

  // Hitung Animasi Clash setelah suspense 1 detik
  setTimeout(() => {
    // 1. Tampilkan Tangan Player 1 (Kiri)
    const choice1 = HAND_MAP[playerChoice];
    if (p1HandImg) {
      p1HandImg.src = choice1.img;
      p1HandImg.className = "hand-svg-graphic clash-in-left";
    }
    if (p1ChoiceText) p1ChoiceText.textContent = choice1.label;
    if (p1LabelPill) p1LabelPill.style.display = "inline-flex";

    // 2. Tampilkan Tangan Komputer (Kanan)
    const choice2 = HAND_MAP[computerChoice];
    if (p2HandImg) {
      p2HandImg.src = choice2.img;
      p2HandImg.className = "hand-svg-graphic flipped-hand clash-in-right";
    }
    if (p2ChoiceText) p2ChoiceText.textContent = choice2.label;
    if (p2LabelPill) p2LabelPill.style.display = "inline-flex";

    // Sembunyikan timer ronde
    if (countdownTimerBox) countdownTimerBox.style.display = "none";

    // 3. Evaluasi Pemenang
    const result = evaluateWinner(playerChoice, computerChoice);
    announceWinner(result);

    // Buka kunci ronde setelah selesai
    setTimeout(() => {
      isPlayingRound = false;
      if (isAutoMode) {
        scheduleNextAutoRound(3500);
      } else {
        if (actionPromptText) actionPromptText.textContent = "PILIH TANGANMU UNTUK RONDE BERIKUTNYA!";
      }
    }, 2500);

  }, 1000);
}

// Logika Evaluasi Pemenang
function evaluateWinner(c1, c2) {
  if (c1 === c2) return "draw";

  if (
    (c1 === "rock" && c2 === "scissors") ||
    (c1 === "scissors" && c2 === "paper") ||
    (c1 === "paper" && c2 === "rock")
  ) {
    p1Score++;
    return "p1";
  } else {
    p2Score++;
    return "p2";
  }
}

// Tampilkan Banner Pemenang
function announceWinner(result) {
  if (p1ScoreEl) p1ScoreEl.textContent = p1Score;
  if (p2ScoreEl) p2ScoreEl.textContent = p2Score;

  if (!winnerDisplayPanel) return;
  winnerDisplayPanel.style.display = "flex";

  if (result === "p1") {
    if (crownIconBox) crownIconBox.style.display = "block";
    if (winnerNameText) winnerNameText.textContent = paramP1Name.toUpperCase();
    if (winnerStatusText) winnerStatusText.textContent = "MENANG!";
  } else if (result === "p2") {
    if (crownIconBox) crownIconBox.style.display = "block";
    if (winnerNameText) winnerNameText.textContent = paramP2Name.toUpperCase();
    if (winnerStatusText) winnerStatusText.textContent = "MENANG!";
  } else {
    if (crownIconBox) crownIconBox.style.display = "none";
    if (winnerNameText) winnerNameText.textContent = "SERI";
    if (winnerStatusText) winnerStatusText.textContent = "SAMA KUAT!";
  }
}

// Penjadwalan Ronde Otomatis (Auto Battle / Showcase Mode)
function scheduleNextAutoRound(delayMs) {
  clearTimeout(autoPlayTimer);
  autoPlayTimer = setTimeout(() => {
    if (isAutoMode && !isPlayingRound) {
      const randomPlayerChoice = CHOICES[Math.floor(Math.random() * CHOICES.length)];
      playRound(randomPlayerChoice);
    }
  }, delayMs);
}

// Reset Skor
function resetGameScores() {
  p1Score = 0;
  p2Score = 0;
  roundCount = 0;
  if (p1ScoreEl) p1ScoreEl.textContent = "0";
  if (p2ScoreEl) p2ScoreEl.textContent = "0";
  if (roundTimerEl) roundTimerEl.textContent = "1";
  resetDuelHands();
}

// Start saat halaman siap
window.addEventListener("DOMContentLoaded", initGame);
