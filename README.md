# Panduan Setup & Integrasi Wokwi

Dokumen ini berisi langkah-langkah untuk menjalankan dashboard monitoring baterai dan menghubungkannya dengan simulator Wokwi melalui port serial virtual.

---

## Kebutuhan Sistem

1. **.NET SDK** (versi 9.0 atau 10.0)
2. **Browser**: Google Chrome atau Microsoft Edge (mendukung Web Serial API)
3. **Emulator Virtual COM Port**: [com0com](https://sourceforge.net/projects/com0com/) atau *Virtual Serial Port Driver (VSPD)*

---

## 1. Setup Virtual COM Port

1. Buka software emulator port virtual (misal: com0com).
2. Buat sepasang port virtual: **`COM1`** dan **`COM2`**.
3. Pastikan kedua port berstatus aktif.

---

## 2. Setup Wokwi

1. Buka sketch simulasi Arduino/ESP32 di Google Chrome atau Microsoft Edge.
2. Pastikan sketch mengirim data sensor melalui Serial dengan baud rate `115200` dan diakhiri `\r\n` (`println`).

### Contoh Kode Sketch (JSON Format)

```cpp
void setup() {
  Serial.begin(115200);
}

void loop() {
  float v = 48.50;    // Tegangan (Volt)
  float i = 12.40;    // Arus (Ampere)
  float t = 31.2;     // Suhu (°C)
  float soc = 86.0;   // Kapasitas / SOC (%)
  float soh = 97.5;   // Kesehatan / SOH (%)

  // Kirim data format JSON
  Serial.print("{\"v\":");
  Serial.print(v, 2);
  Serial.print(",\"i\":");
  Serial.print(i, 2);
  Serial.print(",\"t\":");
  Serial.print(t, 1);
  Serial.print(",\"soc\":");
  Serial.print(soc, 1);
  Serial.print(",\"soh\":");
  Serial.print(soh, 1);
  Serial.println("}");

  delay(200);
}
```

3. Jalankan simulasi di Wokwi.
4. Tekan **F1** di area simulasi, lalu pilih **"Connect to Serial Port"**.
5. Pilih port virtual **`COM2`** lalu klik **Connect**.

---

## 3. Menjalankan Dashboard

1. Buka terminal pada direktori project:
   ```bash
   cd DashboardMonitorBateraiManajemen
   dotnet run
   ```
2. Pada antarmuka aplikasi:
   - Pilih Port: **`COM1`** (pasangan dari `COM2` yang dipakai Wokwi).
   - Pilih Baud Rate: **`115200`**.
   - Klik tombol **HUBUNGKAN**.
3. Data telemetri dari Wokwi akan langsung terbaca pada dashboard.

---

## Format Data yang Didukung

Aplikasi otomatis mengenali format berikut dari Serial:

1. **JSON** (Disarankan):
   ```json
   {"v": 48.5, "i": 12.4, "t": 31.2, "soc": 86.0, "soh": 97.5}
   ```
2. **Key-Value**:
   ```
   V:48.5,I:12.4,T:31.2,SOC:86.0,SOH:97.5
   ```
3. **CSV**:
   ```
   48.5,12.4,31.2,86.0,97.5
   ```
   *(Urutan: Tegangan, Arus, Suhu, SOC, SOH)*

---

## Catatan Tambahan

- Jika port serial belum terhubung ke hardware/Wokwi, tombol **SIMULASI DEMO** dapat digunakan untuk menguji pergerakan UI dan indikator visual.
- Tombol **ikon Matahari/Bulan** di sudut kanan atas digunakan untuk beralih antara Dark Mode dan Light Mode.
