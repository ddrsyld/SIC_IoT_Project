/*
 * =========================================================================================
 * BATERAI MANAGEMENT SYSTEM (BMS) TELEMETRY SIMULATOR UNTUK WOKWI (ARDUINO / ESP32)
 * =========================================================================================
 * Sketch ini menyimulasikan pembacaan sensor baterai:
 * 1. Tegangan (Voltage, V)
 * 2. Arus (Current, A)
 * 3. Suhu (Temperature, °C)
 * 4. SOC (State of Charge, %)
 * 5. SOH (State of Health, %)
 *
 * Dikirim melalui Serial / RS-232 setiap 200 ms (5 Hz) dalam format JSON & CSV.
 * Kompatibel langsung dengan dashboard WPF Monochrome kita!
 * =========================================================================================
 */

// Parameter Awal Baterai
float voltage = 48.60;      // Volt (Nominal Pack 48V / 16S)
float currentVal = 14.50;   // Ampere (+ = Charging, - = Discharging)
float temperature = 31.2;   // Derajat Celsius
float soc = 86.0;           // State of Charge (%)
float soh = 97.8;           // State of Health (%)

unsigned long lastSendTime = 0;
const unsigned long sendInterval = 200; // Kirim tiap 200 ms (5 Hz)

void setup() {
  // Samakan Baud Rate dengan setting di Dashboard WPF (default 115200)
  Serial.begin(115200);
  randomSeed(analogRead(0));
}

void loop() {
  unsigned long currentTime = millis();

  if (currentTime - lastSendTime >= sendInterval) {
    lastSendTime = currentTime;

    // 1. Simulasikan fluktuasi sensor yang realistis
    float noiseV = (random(-15, 16)) / 100.0;     // Fluktuasi ±0.15V
    voltage += noiseV;
    if (voltage < 42.0) voltage = 42.0;
    if (voltage > 54.6) voltage = 54.6;

    float noiseI = (random(-80, 81)) / 100.0;     // Fluktuasi ±0.8A
    currentVal += noiseI;
    if (currentVal < -25.0) currentVal = -25.0;
    if (currentVal > 25.0)  currentVal = 25.0;

    float noiseT = (random(-5, 7)) / 100.0;       // Fluktuasi suhu
    temperature += noiseT;
    if (temperature < 20.0) temperature = 20.0;
    if (temperature > 55.0) temperature = 55.0;

    // SOC bergerak perlahan mengikuti arus
    soc += (currentVal * 0.002);
    if (soc < 5.0)  soc = 5.0;
    if (soc > 100.0) soc = 100.0;

    // SOH berkurang sangat lambat seiring siklus
    soh -= 0.0001;
    if (soh < 60.0) soh = 60.0;

    /*
     * FORMAT PENGIRIMAN:
     * Dashboard WPF kita secara otomatis mengenali 3 format:
     * - Opsi A (JSON): {"v":48.6,"i":14.5,"t":31.2,"soc":86.0,"soh":97.8}
     * - Opsi B (Key-Value): V:48.6,I:14.5,T:31.2,SOC:86.0,SOH:97.8
     * - Opsi C (CSV): 48.6,14.5,31.2,86.0,97.8
     *
     * Di sini kita gunakan format JSON standar industri:
     */
    Serial.print("{\"v\":");
    Serial.print(voltage, 2);
    Serial.print(",\"i\":");
    Serial.print(currentVal, 2);
    Serial.print(",\"t\":");
    Serial.print(temperature, 1);
    Serial.print(",\"soc\":");
    Serial.print(soc, 1);
    Serial.print(",\"soh\":");
    Serial.print(soh, 1);
    Serial.println("}");
  }
}
