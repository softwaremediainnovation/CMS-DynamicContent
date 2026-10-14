# CmsBillboard — Dynamic Content Server (DOOH Interactive)

Platform Dynamic Content berbasis **ASP.NET Core (.NET 9)** dan **SignalR** untuk videotron komersial & interaksi audiens smartphone (Warna Warni Advertising).

---

## 🚀 Fitur Aktif

### Modul 7: Advertising Content + Survey / Quiz (Pilihan Ganda)
- **Tampilan Layar Videotron (LED Screen)**:
  - Tampilan split-screen fullscreen: Materi iklan komersial (kiri) + Pertanyaan survey & grafik hasil live (kanan).
  - Dynamic QR Code (SVG) yang mengarahkan audiens ke halaman voting smartphone.
  - Bar chart animasi realtime terhubung via SignalR (`/hubs/survey`).
  - Indikator total suara/partisipan live.
- **Tampilan Smartphone Audiens**:
  - **Mode 1: On-Premise (`mode=onpremise`)**: Notifikasi melihat soal di layar videotron + tombol pilihan A/B/C/D sentuh besar.
  - **Mode 2: Full (`mode=full`)**: Pertanyaan lengkap dan pilihan opsi A/B/C/D di layar ponsel.
  - Proteksi anti-spam: **Strict 1-Time Participation** per perangkat/sesi (Device Fingerprint + IP).

---

## 🛠️ Arsitektur & Teknologi

- **Backend**: ASP.NET Core Web API + Razor Pages (.NET 9)
- **Realtime Push**: ASP.NET Core SignalR
- **Database**: SQLite via Entity Framework Core 9 (`DynamicContent.db`)
- **QR Code Engine**: QRCoder (SVG generation)
- **Frontend**: Responsive HTML5, CSS3, Vanilla JS, SignalR Client (Offline Ready)

---

## 📍 Rute & Endpoint

| Endpoint | Keterangan |
|---|---|
| `/` | Dashboard Hub & Pengujian |
| `/Screen/Survey?id={surveyId}&site={siteId}` | Tampilan Layar Videotron LED |
| `/Vote?id={surveyId}&site={siteId}&mode={mode}` | Halaman Voting Smartphone Audiens |
| `/api/survey/{id}/results?site={siteId}` | REST API Hasil Survey & Skor |
| `/api/survey/vote` | REST API Submit Vote |
| `/hubs/survey` | SignalR Realtime Hub |

---

## 🏃 Cara Menjalankan

```bash
dotnet restore
dotnet run --urls "http://localhost:5055"
```
Buka browser di: `http://localhost:5055`
