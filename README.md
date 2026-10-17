# 🔋 BMS Telemetry Dashboard & Panduan Koneksi ke Wokwi

Halo! Project ini adalah aplikasi desktop (WPF .NET) untuk memonitor kondisi baterai secara *real-time* (Tegangan, Arus, Suhu, SOC, dan SOH). Tampilannya mengusung tema monokrom modern (lengkap dengan Dark/Light mode) serta animasi baterai yang bisa bergerak dan berubah warna dinamis dari **Hijau $\to$ Kuning $\to$ Merah**.

Panduan ini dibuat supaya kamu bisa langsung menjalankan dashboard ini di laptop dan menghubungkannya dengan simulasi rangkaian yang kamu buat di **Wokwi**.

---

## 💡 Konsep Singkat (Cara Kerjanya)

Karena Wokwi jalan di browser dan dashboard ini adalah aplikasi desktop, keduanya dihubungkan menggunakan **Kabel Virtual (Virtual COM Port Pair)** di laptop:

```
[ Wokwi (Chrome / Edge) ]
         │ (Web Serial API)
         ▼
    [ COM2 ]  <─── Sepasang COM Port Virtual (com0com / VSPD)
         ▲
         │ (System.IO.Ports)
[ Dashboard WPF (.NET) ]
```
> Apapun yang dikirim oleh Arduino/ESP32 di Wokwi ke `COM2`, otomatis diterima oleh Dashboard WPF di `COM1`.

---

## 🛠️ Persiapan Awal

Sebelum mulai, pastikan laptop kamu sudah ada:
1. **.NET SDK** (.NET 10 atau .NET 9). Cek di CMD/Terminal dengan ketik: `dotnet --version`.
2. **Browser Google Chrome atau Microsoft Edge** (wajib, karena fitur serial Wokwi hanya jalan di browser Chromium).
3. **Software Virtual COM Port**, salah satu dari:
   - [com0com](https://sourceforge.net/projects/com0com/) (*Gratis & Open Source*)
   - Atau *Virtual Serial Port Driver (VSPD)* / *Eterlogic VSPE*.

---

## 🚀 Langkah 1: Buat Pasangan Port COM Virtual

1. Buka aplikasi **com0com** atau **VSPD**.
2. Buat satu pasang port baru, misalnya: **`COM1`** dan **`COM2`**.
3. Pastikan status keduanya aktif / *connected*.

---

## 💻 Langkah 2: Setting di Wokwi (Kodingan Arduino / ESP32)

Dashboard ini pintar dan sudah bisa baca 3 format data sekaligus (JSON, CSV, atau Key-Value). Format yang paling direkomendasikan adalah **JSON**.

### Contoh Kodingan Sketch Wokwi:
Kamu bisa copas contoh kodingan ini ke sketch Wokwi kamu (tersedia juga di folder `wokwi_sketch/bms_wokwi_simulation.ino`):

```cpp
void setup() {
  // Samakan baud rate dengan dashboard (default 115200)
  Serial.begin(115200);
}

void loop() {
  // 1. Data bacaan sensor kamu
  float voltage = 48.5;       // Tegangan (Volt)
  float current = 12.4;       // Arus (+ = ngecas/charge, - = kepake/discharge)
  float temperature = 31.2;   // Suhu (°C)
  float soc = 86.0;           // Kapasitas sisa baterai (0 - 100%)
  float soh = 97.5;           // Kesehatan sel baterai (0 - 100%)

  // 2. Kirim ke Serial dalam format JSON (wajib diakhiri println)
  Serial.print("{\"v\":");
  Serial.print(voltage, 2);
  Serial.print(",\"i\":");
  Serial.print(current, 2);
  Serial.print(",\"t\":");
  Serial.print(temperature, 1);
  Serial.print(",\"soc\":");
  Serial.print(soc, 1);
  Serial.print(",\"soh\":");
  Serial.print(soh, 1);
  Serial.println("}");

  delay(200); // Kirim setiap 200ms (5x per detik)
}
```

### Cara Connect Wokwi ke Port COM Virtual:
1. Jalankan simulasi di Wokwi (klik tombol **Play/Start Simulation** hijau).
2. Tekan tombol **F1** di keyboard saat kursor ada di area Wokwi (atau klik menu titik tiga di panel Serial Monitor Wokwi).
3. Ketik dan pilih **"Connect to Serial Port"**.
4. Browser akan memunculkan popup daftar COM port $\to$ pilih **`COM2`** lalu klik **Connect**.

---

## 🖥️ Langkah 3: Menjalankan Dashboard Monitoring

1. Buka terminal (PowerShell / Command Prompt) di folder project ini:
   ```bash
   cd DashboardMonitorBateraiManajemen
   dotnet run
   ```
   *(Atau bisa juga buka file solusinya lewat Visual Studio lalu tekan F5 / Play).*

2. Di tampilan Dashboard:
   - Pilih **Port COM**: pilih pasangannya, yaitu **`COM1`** (kalau belum muncul, klik tombol ikon putar di sebelahnya untuk refresh).
   - Pilih **Baud Rate**: **`115200`** (harus sama persis dengan `Serial.begin` di Wokwi).
   - Klik tombol hitam **"HUBUNGKAN"**.

3. **Selesai!** Status akan berubah menjadi **TERKONEKSI (ONLINE)**, data Wokwi akan langsung masuk secara live, grafik waveform bergerak, dan visual baterai akan terisi otomatis.

---

## 🎨 Fitur Keren di Dashboard Ini

- **Animasi Baterai Vertikal:** Level cairan baterai naik-turun otomatis sesuai nilai SOC%. Warnanya otomatis berubah:
  - 🟢 **Hijau**: Baterai penuh / aman (> 50%).
  - 🟡 **Kuning/Oranye**: Baterai mulai berkurang (20% - 50%).
  - 🔴 **Merah**: Baterai kritis / hampir habis (< 20%).
  - Muncul ikon petir otomatis kalau arusnya positif (lagi charging).
- **Dual Arc Gauge:** Meteran melingkar untuk State of Charge (SOC) dan State of Health (SOH).
- **Waveform Grafik Real-Time:** Grafik kurva 60 detik terakhir untuk melihat fluktuasi Tegangan (V), Arus (A), dan Suhu (°C).
- **Dark / Light Mode:** Klik ikon **Bulan / Matahari** di pojok kanan atas buat ganti tema.
- **Mode Simulasi Demo:** Kalau lagi ga buka Wokwi tapi mau pamerin/ngetes UI-nya ke orang lain, cukup klik tombol **"SIMULASI DEMO"** di atas. Semua sensor bakal bergerak otomatis dengan data simulasi yang realistis.
- **Terminal Log Raw RS-232:** Di bagian bawah ada log terminal live buat ngecek data mentah apa yang lagi dikirim Wokwi. Bisa diminimize/expand.

---

## ❓ Troubleshooting (Kendala yang Sering Terjadi)

* **Port COM ga muncul di dropdown?**
  Pastikan software virtual COM port sudah dijalankan dan port pair sudah dibuat, lalu klik tombol **Refresh (ikon putar)** di samping dropdown COM.
* **Wokwi ga bisa connect ke port serial?**
  Pastikan kamu pakai browser **Google Chrome** atau **Microsoft Edge**. Browser lain seperti Mozilla Firefox belum mendukung *Web Serial API*.
* **Dashboard sudah 'Terkoneksi' tapi angkanya ga berubah?**
  1. Cek apakah Baud Rate di Wokwi (`Serial.begin(115200)`) sudah sama dengan di Dashboard (`115200`).
  2. Pastikan pengiriman data di Wokwi pakai `Serial.println()` (mengandung baris baru/enter).
  3. Cek kotak "Terminal Log Raw" di bagian bawah dashboard untuk melihat apakah teks dari Wokwi berhasil masuk.

---

*Selamat mencoba & semoga lancar pengujiannya! Kalau ada ide fitur tambahan, silakan dikembangkan bareng-bareng.* 🚀
