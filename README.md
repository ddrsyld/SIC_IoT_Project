# 🔋 BMS Telemetry & Battery Capacity (Ah) Dashboard
> **Sistem Monitoring Baterai Real-Time berbasis WPF (.NET 10) & Integrasi Simulator Wokwi via MQTT & Serial**

---

## 💡 Jawaban: Kenapa Bisa Pakai MQTT Tanpa Port Serial / COM?

Banyak yang mengira simulator Wokwi di browser web hanya bisa kirim data jika ada port COM fisik di laptop. **Itu tidak benar jika menggunakan ESP32!**

### 🌐 Cara Kerja Integrasi Wokwi + MQTT:
```
+-------------------------------------------------------+
| SIMULATOR WOKWI (Browser: Chrome / Edge / Firefox)    |
| - Board: ESP32                                        |
| - WiFi Bawaan Wokwi: "Wokwi-GUEST" (Akses Internet)   |
| - PubSubClient mem-publish JSON ke:                   |
|   Topic: "sic/bms/telemetry"                          |
+---------------------------+---------------------------+
                            | (Internet / TCP Port 1883)
                            v
+-------------------------------------------------------+
| PUBLIC MQTT BROKER (Cloud EMQX)                       |
| - Host: broker.emqx.io                                |
| - Port: 1883 (Gratis, Publik, Tanpa Registrasi)      |
+---------------------------+---------------------------+
                            | (Internet / TCP Port 1883)
                            v
+-------------------------------------------------------+
| DASHBOARD WPF DESKTOP (.NET 10)                       |
| - MqttService (MQTTnet v5) otomatis Subscribe         |
| - Mengalirkan data Tegangan, Arus, Suhu, SOC, & Ah   |
| - Waveform Graph & Gauge bergerak Real-time!          |
+-------------------------------------------------------+
```

### 🎯 Keunggulan Metode MQTT Ini:
1. **100% Bebas Driver Virtual COM**: Tidak perlu install *com0com*, *VSPD*, atau driver pihak ketiga apa pun.
2. **Bisa di Semua Browser**: Tidak bergantung pada Web Serial API Google Chrome. Firefox, Edge, Safari, dan Chrome semua bisa.
3. **Bisa Lintas Perangkat**: Simulasi Wokwi bisa dijalankan di laptop lain, HP, atau tablet, dan datanya tetap masuk ke dashboard WPF di PC Anda secara instan selama ada koneksi internet.

---

## ⚡ Panduan Langkah demi Langkah: Integrasi Wokwi (MQTT)

### Langkah 1: Siapkan Simulasi di Wokwi
1. Buka browser dan kunjungi: **[https://wokwi.com/projects/new/esp32](https://wokwi.com/projects/new/esp32)** (Pilih board **ESP32**).
2. Di dalam proyek Wokwi Anda:
   * **Tab Sketch (`sketch.ino` atau `bms_wokwi_simulation.ino`)**:
     Hapus seluruh isi default, lalu salin (*copy-paste*) kode dari:
     [`wokwi_sketch/bms_wokwi_simulation.ino`](file:///D:/dotnet/WPF/DashboardSIC1/DashboardMonitorBateraiManajemen/wokwi_sketch/bms_wokwi_simulation.ino)
   * **Tab `diagram.json`**:
     Salin isi dari:
     [`wokwi_sketch/diagram.json`](file:///D:/dotnet/WPF/DashboardSIC1/DashboardMonitorBateraiManajemen/wokwi_sketch/diagram.json)
   * **Tab Library Manager (`libraries.txt`)**:
     Klik ikon tab dokumen bertanda `+` di Wokwi, beri nama file `libraries.txt`, lalu ketik:
     ```
     PubSubClient
     ```
3. Klik tombol **Play (Hijau) / Start Simulation**.
4. Perhatikan Serial Monitor di bagian bawah Wokwi. Anda akan melihat output:
   ```text
   [WiFi] Menghubungkan ke Wokwi-GUEST...
   [WiFi] Terhubung! IP Address: 10.0.1.2
   [MQTT] Menghubungkan ke broker: broker.emqx.io... Berhasil terhubung!
   [MQTT PUBLISH] {"v":48.60,"i":14.50,"t":31.2,"soc":86.0,"ah":86.0}
   ```
   *Artinya ESP32 Wokwi sudah sukses mengirim data ke internet!*

---

### Langkah 2: Jalankan Dashboard WPF
Anda bisa menjalankan aplikasi menggunakan salah satu cara di bawah:

#### **Opsi A: Langsung Buka File .EXE (Siap Pakai, Tanpa Perlu Install SDK)**
1. Masuk ke folder:
   [`DashboardMonitorBateraiManajemen/publish/`](file:///D:/dotnet/WPF/DashboardSIC1/DashboardMonitorBateraiManajemen/publish/)
2. Klik dua kali file **`DashboardMonitorBateraiManajemen.exe`**.

#### **Opsi B: Menggunakan Terminal (.NET SDK)**
Buka PowerShell/Terminal di direktori ini:
```bash
cd DashboardMonitorBateraiManajemen
dotnet run
```

---

### Langkah 3: Hubungkan Dashboard ke MQTT
1. Pada bilah kontrol bagian atas dashboard:
   * **Pilihan Mode**: Pilih **`MQTT (Wokwi / IoT)`**
   * **Broker**: Biarkan default `broker.emqx.io` (atau ketik broker lain jika diinginkan)
   * **Topic**: Biarkan default `sic/bms/telemetry`
2. Klik tombol **`HUBUNGKAN`**.
3. Status badge di sebelah kanan tombol akan menyala hijau bertuliskan **`TERKONEKSI (MQTT)`**.
4. **Selesai!** Seluruh grafik tegangan, arus, suhu, persentase SOC, dan **Deteksi Kapasitas (Ah)** akan langsung ter-update secara dinamis mengikuti sensor baterai dari Wokwi.

---

## 🔋 Pembaruan Metrik: SOH Dihilangkan & Diganti Deteksi Ah

Sesuai spesifikasi monitoring sistem baterai:
* **SOH (State of Health) telah dihapus sepenuhnya** dari model, logika, dan visualisasi UI.
* **Digantikan dengan Deteksi Kapasitas Ah (Ampere-Hour / Coulomb Counting)**:
  * **Card KPI ke-5 (Pojok Kanan Atas)**:
    * Judul: `DETEKSI KAPASITAS` (Nominal 100 Ah pack)
    * Angka Utama: Menampilkan nilai kapasitas terisi terkini (misal: **`86.0 Ah`**)
    * Visualisasi Mini Track: Menampilkan level sisa kapasitas (Ah / 100 Ah)
    * Status: `OPTIMAL (>80 Ah)`, `NORMAL (40-80 Ah)`, atau `RENDAH (<40 Ah)`
  * **Radial Gauge Panel Kiri**:
    * Arc gauge melingkar kini memvisualisasikan kapasitas aktif baterai secara real-time dengan pembacaan angka `Ah` di bagian tengahnya.
  * **Format Payload JSON**:
    ```json
    {
      "v": 48.60,
      "i": 14.50,
      "t": 31.2,
      "soc": 86.0,
      "ah": 86.0
    }
    ```

---

## 🔌 Alternatif: Mode Serial (RS-232 / Port COM Fisik)

Jika Anda memiliki mikrokontroler fisik asli (Arduino / ESP32 / STM32) yang dicolokkan ke port USB laptop Anda:
1. Ubah dropdown mode di dashboard ke **`SERIAL (RS-232 / COM)`**.
2. Pilih port COM perangkat Anda (misal `COM3`, `COM4`, dsb.).
3. Pilih Baud Rate (default: `115200`).
4. Klik tombol **`HUBUNGKAN`**.
5. Dashboard otomatis mendeteksi format data dari serial, baik berupa JSON, Key-Value (`AH:86.0`), maupun CSV.

---

## 🛠️ Ringkasan Fitur Tambahan Dashboard
* **Simulasi Demo Internal**: Jika Anda sedang offline / tidak membuka Wokwi, klik tombol **`SIMULASI DEMO`** untuk menguji animasi grafik dan indikator baterai.
* **Mode Gelap / Terang (Dark / Light)**: Klik tombol ikon Bulan / Matahari di pojok kanan atas untuk beralih tema monochrome.
* **Terminal Log Real-Time**: Bagian bawah dashboard dilengkapi terminal monitor untuk melihat setiap paket JSON mentah yang masuk beserta timestamp milidetik.
* **Pencegahan Konflik Port / Network**: Tombol *disconnect* otomatis memutus koneksi socket/serial secara rapi saat berpindah mode atau menutup aplikasi.

---

## ❓ FAQ & Troubleshooting

| Pertanyaan / Masalah | Penyebab | Solusi |
| :--- | :--- | :--- |
| **Wokwi tidak mau konek ke MQTT** | Library `PubSubClient` belum terpasang di Wokwi | Buat file `libraries.txt` di Wokwi dan tuliskan teks `PubSubClient` di dalamnya. |
| **Dashboard status "Terputus dari Broker"** | Koneksi internet laptop terputus atau firewall memblokir port 1883 | Pastikan laptop terhubung internet. Port 1883 adalah port standar MQTT TCP publik. |
| **Nilai Ah tidak bergerak** | Arus berada di 0.0 A (mode Standby) | Ubah nilai arus di sketch Wokwi atau biarkan fluktuasi berjalan untuk melihat Coulomb counting. |
| **Ingin ganti topic MQTT sendiri** | Menghindari tabrakan data dengan pengguna publik lain | Ubah nama topic di sketch (misal `bms/mybms123`) dan samakan isian Topic di Dashboard. |
