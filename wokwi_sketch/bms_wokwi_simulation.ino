#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <math.h>
#include <WiFi.h>
#include <PubSubClient.h>



// ================= PIN =================

#define ACS712_PIN          34

#define VOLTAGE_SENSOR_PIN  35

#define NTC_PIN             32

#define RELAY_PIN           26

#define BUZZER_PIN          27
#define RESET_BUTTON_PIN     25  // Tombol reset: GPIO25 ke 3V3, gunakan INPUT_PULLDOWN



#define OLED_SDA            21

#define OLED_SCL            22



// ================= OLED =================

#define SCREEN_WIDTH  128

#define SCREEN_HEIGHT 64

#define OLED_ADDR     0x3C



Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, -1);



// ================= ADC =================

const float ADC_REF = 3.3f;

const int ADC_MAX = 4095;



// ================= KALIBRASI ACS712 =================

const float ACS_ZERO_VOLTAGE = 1.6507f;  // Koreksi titik nol berdasarkan uji slider 0 A

const float ACS_SENSITIVITY = 0.0825f;

const float ACS_CALIBRATION_FACTOR = 1.51444f;

const float ACS_CURRENT_MIN_A = -30.0f;
const float ACS_CURRENT_MAX_A =  30.0f;

const float ACS_OUTPUT_MIN_V = 0.0f;
const float ACS_OUTPUT_MAX_V = 3.3f;



// ================= SENSOR TEGANGAN =================

const float VOLTAGE_CALIBRATION_FACTOR = 1.51515f;

const float VOLTAGE_OFFSET = 0.0f;



// ================= NTC 10K =================

// Rumus NTC dipertahankan sesuai sketch yang sudah berhasil di Wokwi.

const float NTC_NOMINAL = 10000.0f;

const float TEMP_NOMINAL = 25.0f;

const float BETA = 3950.0f;

const float SERIES_RESISTOR = 10000.0f;



// ================= BATERAI =================

const float BATTERY_CAPACITY_AH = 100.0f; // LiFePO4 12.8 V 100 Ah



// Tabel SOC perkiraan berdasarkan tegangan istirahat.

// Nilai 13.0 V pada tabel sebelumnya berada di antara 40% dan 50%,

// sehingga digabung menjadi 45% agar interpolasi tidak ambigu.

const int SOC_TABLE_SIZE = 10;

const float socVoltageTable[SOC_TABLE_SIZE] = {

  10.0f, 12.0f, 12.8f, 12.9f, 13.0f,

  13.1f, 13.2f, 13.3f, 13.4f, 13.6f

};

const float socPercentTable[SOC_TABLE_SIZE] = {

  0.0f, 10.0f, 20.0f, 30.0f, 45.0f,

  60.0f, 70.0f, 80.0f, 90.0f, 100.0f

};



// ================= BATAS PROTEKSI =================

const float MAX_CURRENT = 20.0f;  // Fault jika arus > +20 A atau < -20 A; target tampilan sensor hingga +/-30 A

const float MAX_VOLTAGE = 24.0f;

const float MAX_TEMPERATURE = 60.0f;



// Thermal runaway: suhu >=45°C dan kenaikan >=1.5°C/detik.

const float RAPID_RISE_START_TEMP = 45.0f;

const float MAX_TEMP_RISE_C_PER_SEC = 1.5f;



// Relay modul aktif LOW.

const bool RELAY_ACTIVE_LOW = false;



// ================= VARIABEL =================

float current = 0.0f;

float inputVoltage = 0.0f;

float temperatureC = NAN;

float SOC = 0.0f;

float dischargedAh = 0.0f; // Total Ah yang keluar sejak ESP32 mulai/reset



bool thermalRunaway = false;
bool overCurrentFault = false;
bool overVoltageFault = false;
bool alarmActive = false;



float previousTemperatureC = NAN;

unsigned long previousTemperatureMs = 0;

float temperatureRiseRate = 0.0f;

unsigned long lastAhUpdateMs = 0;

unsigned long thermalSafeSinceMs = 0;

const float THERMAL_RESET_TEMP = 50.0f; // Suhu harus di bawah nilai ini agar reset diterima

// Debounce tombol reset
const unsigned long BUTTON_DEBOUNCE_MS = 40UL;
bool resetButtonStableState = LOW;
bool resetButtonLastReading = LOW;
unsigned long resetButtonLastChangeMs = 0;

// ================= WIFI & MQTT =================
const char* WIFI_SSID     = "Wokwi-GUEST";
const char* WIFI_PASSWORD = "";
const char* MQTT_SERVER   = "broker.emqx.io";
const int   MQTT_PORT     = 1883;
const char* MQTT_TOPIC    = "sic/bms/telemetry";

WiFiClient espClient;
PubSubClient mqttClient(espClient);
unsigned long lastMqttSendMs = 0;
const unsigned long MQTT_INTERVAL_MS = 300; // Kirim telemetri tiap 300 ms

void setupWiFi() {
  Serial.print("[WiFi] Menghubungkan ke ");
  Serial.println(WIFI_SSID);
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);
  int waitCount = 0;
  while (WiFi.status() != WL_CONNECTED && waitCount < 15) {
    delay(200);
    Serial.print(".");
    waitCount++;
  }
  if (WiFi.status() == WL_CONNECTED) {
    Serial.println("\n[WiFi] Terhubung! IP: " + WiFi.localIP().toString());
  } else {
    Serial.println("\n[WiFi] Koneksi berlanjut di background...");
  }
}

void checkMqttConnection() {
  if (WiFi.status() != WL_CONNECTED) {
    return;
  }
  if (!mqttClient.connected()) {
    String clientId = "ESP32_BMS_SIC_" + String(random(0xffff), HEX);
    if (mqttClient.connect(clientId.c_str())) {
      Serial.println("[MQTT] Terhubung ke broker.emqx.io!");
    }
  }
}

// ================= RELAY =================

void setRelay(bool active) {

  digitalWrite(

    RELAY_PIN,

    active

      ? (RELAY_ACTIVE_LOW ? LOW : HIGH)

      : (RELAY_ACTIVE_LOW ? HIGH : LOW)

  );

}



// ================= ADC RATA-RATA =================

float readADC(uint8_t pin) {

  const int samples = 50;

  uint32_t sum = 0;



  for (int i = 0; i < samples; i++) {

    sum += analogRead(pin);

    delay(2);

  }



  return sum / (float)samples;

}



// ================= SOC DARI TABEL TEGANGAN =================

float calculateSOC(float voltage) {

  if (!isfinite(voltage)) return NAN;



  if (voltage <= socVoltageTable[0]) return socPercentTable[0];

  if (voltage >= socVoltageTable[SOC_TABLE_SIZE - 1])

    return socPercentTable[SOC_TABLE_SIZE - 1];



  for (int i = 0; i < SOC_TABLE_SIZE - 1; i++) {

    if (voltage <= socVoltageTable[i + 1]) {

      float fraction =

        (voltage - socVoltageTable[i]) /

        (socVoltageTable[i + 1] - socVoltageTable[i]);



      return socPercentTable[i] +

        fraction * (socPercentTable[i + 1] - socPercentTable[i]);

    }

  }



  return 100.0f;

}



// ================= OLED =================

void updateOLED(float voltage, float amps, float temperature,

                float soc, float ah, bool fault) {

  display.clearDisplay();

  display.setTextColor(SSD1306_WHITE);

  display.setTextSize(1);



  display.setCursor(0, 0);

  display.println("MONITORING BATERAI");

  display.drawLine(0, 9, 127, 9, SSD1306_WHITE);



  display.setCursor(0, 12);

  display.print("V:");

  display.print(voltage, 2);

  display.print("V I:");

  display.print(amps, 1);

  display.println("A");



  display.setCursor(0, 23);

  display.print("T:");

  if (isfinite(temperature)) {

    display.print(temperature, 1);

    display.print("C");

  } else {

    display.print("INVALID");

  }

  display.print(" SOC:");

  if (isfinite(soc)) {

    display.print(soc, 0);

    display.println("%");

  } else {

    display.println("--");

  }



  display.setCursor(0, 34);

  display.print("Ah keluar:");

  display.print(ah, 3);



  display.drawLine(0, 45, 127, 45, SSD1306_WHITE);

  display.setCursor(0, 50);

  display.print("STATUS: ");

  display.println(fault ? "FAULT!" : "NORMAL");



  display.display();

}



// ================= TOMBOL RESET (DEBOUNCE) =================
// Menghasilkan true satu kali saat tombol baru ditekan.
bool resetButtonPressed() {
  bool reading = digitalRead(RESET_BUTTON_PIN);
  unsigned long now = millis();

  if (reading != resetButtonLastReading) {
    resetButtonLastChangeMs = now;
    resetButtonLastReading = reading;
  }

  if ((now - resetButtonLastChangeMs) >= BUTTON_DEBOUNCE_MS &&
      reading != resetButtonStableState) {
    resetButtonStableState = reading;
    if (resetButtonStableState == HIGH) {
      return true;
    }
  }
  return false;
}

// ================= SETUP =================

void setup() {

  Serial.begin(115200);

  analogReadResolution(12);



  analogSetPinAttenuation(ACS712_PIN, ADC_11db);

  analogSetPinAttenuation(VOLTAGE_SENSOR_PIN, ADC_11db);

  analogSetPinAttenuation(NTC_PIN, ADC_11db);



  pinMode(RELAY_PIN, OUTPUT);

  pinMode(BUZZER_PIN, OUTPUT);
  pinMode(RESET_BUTTON_PIN, INPUT_PULLDOWN);



  setRelay(true);

  noTone(BUZZER_PIN);



  Wire.begin(OLED_SDA, OLED_SCL);



  if (!display.begin(SSD1306_SWITCHCAPVCC, OLED_ADDR)) {

    Serial.println("OLED tidak terdeteksi!");

  } else {

    display.clearDisplay();

    display.setTextSize(1);

    display.setTextColor(SSD1306_WHITE);

    display.setCursor(0, 20);

    display.println("System Starting...");

    display.display();

  }



  Serial.println("Monitoring baterai dimulai.");

  Serial.println("Thermal fault: >=60C, atau >=45C dengan kenaikan >=1.5C/s, atau sensor NTC invalid.");
  Serial.println("Overcurrent: fault jika arus > +20 A atau < -20 A.");
  Serial.println("Rentang pembacaan yang diinginkan: -30 A hingga +30 A; hasil aktual bergantung pada output Custom Chip dan kalibrasi ACS712.");

  Serial.println("Tombol reset GPIO25: semua fault terkunci sampai tombol ditekan saat kondisi aman.");

  previousTemperatureMs = millis();
  lastAhUpdateMs = millis();

  // Inisialisasi WiFi & MQTT
  setupWiFi();
  mqttClient.setServer(MQTT_SERVER, MQTT_PORT);
}



// ================= LOOP =================

void loop() {
  checkMqttConnection();
  mqttClient.loop();

  // Baca sensor dengan rata-rata.

  float acsADC = readADC(ACS712_PIN);

  float voltageADC = readADC(VOLTAGE_SENSOR_PIN);

  float ntcADC = readADC(NTC_PIN);



  // -------- ACS712 --------

  // Wokwi virtual ADC menggunakan skala referensi 5 V untuk pembacaan ADC.
  float acsVoltage = acsADC * 5.0f / ADC_MAX;

  // Custom Chip: 0.0 V -> -30 A, 1.65 V -> 0 A, 3.3 V -> +30 A.
  // Linear conversion from output voltage to current.
  current = ACS_CURRENT_MIN_A +
            ((acsVoltage - ACS_OUTPUT_MIN_V) /
             (ACS_OUTPUT_MAX_V - ACS_OUTPUT_MIN_V)) *
            (ACS_CURRENT_MAX_A - ACS_CURRENT_MIN_A);

  // Small deadband around zero to suppress ADC noise.
  if (current > -0.15f && current < 0.15f) current = 0.0f;

  // Pembulatan arus menjadi 2 angka desimal
  current = roundf(current * 100.0f) / 100.0f;



  // -------- SENSOR TEGANGAN --------

  float sensorOutputVoltage = voltageADC * ADC_REF / ADC_MAX;

  inputVoltage = (sensorOutputVoltage * (25.0f / 3.3f) - VOLTAGE_OFFSET)

                 * VOLTAGE_CALIBRATION_FACTOR;



  // -------- NTC --------

  temperatureC = NAN;

  if (ntcADC > 0.0f && ntcADC < ADC_MAX) {

    // Dipertahankan sesuai sketch yang sudah terbukti cocok dengan Wokwi.

    float resistance = SERIES_RESISTOR * ntcADC / (ADC_MAX - ntcADC);



    if (isfinite(resistance) && resistance > 0.0f) {

      float steinhart = log(resistance / NTC_NOMINAL);

      steinhart /= BETA;

      steinhart += 1.0f / (TEMP_NOMINAL + 273.15f);

      if (isfinite(steinhart) && steinhart > 0.0f) {

        float calculatedTemp = (1.0f / steinhart) - 273.15f;

        if (isfinite(calculatedTemp) &&

            calculatedTemp >= -40.0f && calculatedTemp <= 150.0f) {

          temperatureC = calculatedTemp;

        }

      }

    }

  }



  // -------- SOC --------

  // SOC dari tabel tegangan adalah perkiraan dan paling cocok saat baterai istirahat.

  SOC = calculateSOC(inputVoltage);



  // -------- AH KELUAR: INTEGRASI ARUS DISCHARGE --------

  unsigned long now = millis();

  float dtHours = (now - lastAhUpdateMs) / 3600000.0f;

  // Konvensi program: arus positif = discharge, arus negatif = charging.

  // Ah keluar hanya bertambah ketika arus positif.

  if (isfinite(current) && current > 0.0f && dtHours >= 0.0f) {

    dischargedAh += current * dtHours;

  }

  lastAhUpdateMs = now;



  // -------- LAJU KENAIKAN SUHU --------

  temperatureRiseRate = 0.0f;

  if (isfinite(temperatureC)) {

    if (isfinite(previousTemperatureC) && now > previousTemperatureMs) {

      float dtSeconds = (now - previousTemperatureMs) / 1000.0f;

      if (dtSeconds > 0.0f) {

        temperatureRiseRate = (temperatureC - previousTemperatureC) / dtSeconds;

      }

    }

    previousTemperatureC = temperatureC;

    previousTemperatureMs = now;

  }



  // -------- PROTEKSI --------

  bool overCurrent = (current > MAX_CURRENT) || (current < -MAX_CURRENT);

  bool overVoltage = inputVoltage > MAX_VOLTAGE;

  bool invalidTemperature = !isfinite(temperatureC);

  bool overTemperature = isfinite(temperatureC) &&

                         temperatureC >= MAX_TEMPERATURE;

  bool rapidTemperatureRise = isfinite(temperatureC) &&

                              temperatureC >= RAPID_RISE_START_TEMP &&

                              temperatureRiseRate >= MAX_TEMP_RISE_C_PER_SEC;



  // Semua fault dikunci (latch) setelah terpicu dan hanya dihapus oleh tombol RESET.
  if (overCurrent) {
    overCurrentFault = true;
  }

  if (overVoltage) {
    overVoltageFault = true;
  }

  // Thermal fault aktif jika sensor NTC invalid atau suhu menunjukkan kondisi berbahaya.
  if (invalidTemperature || overTemperature || rapidTemperatureRise) {
    thermalRunaway = true;
  }

  // -------- RESET MANUAL DENGAN TOMBOL GPIO25 --------
  // Reset hanya diterima jika arus, tegangan, dan suhu sudah aman.
  bool resetConditionsSafe =
      isfinite(current) &&
      fabs(current) <= MAX_CURRENT &&
      isfinite(inputVoltage) &&
      inputVoltage > 0.0f &&
      inputVoltage <= MAX_VOLTAGE &&
      isfinite(temperatureC) &&
      temperatureC < THERMAL_RESET_TEMP &&
      !rapidTemperatureRise;

  if (resetButtonPressed()) {
    if (resetConditionsSafe) {
      thermalRunaway = false;
      overCurrentFault = false;
      overVoltageFault = false;
      Serial.println("RESET diterima: thermal runaway, overvoltage, dan overcurrent dihapus secara manual.");
    } else {
      Serial.println("RESET ditolak: kondisi belum aman. Periksa arus, tegangan, suhu, dan sensor NTC.");
    }
  }

  alarmActive = overCurrentFault || overVoltageFault || thermalRunaway;


  // Modul relay dikonfigurasi aktif HIGH:

  // normal -> relay ON; fault -> relay OFF.

  setRelay(!alarmActive);

  if (alarmActive) {

    tone(BUZZER_PIN, 2000);

  } else {

    noTone(BUZZER_PIN);

  }



  // -------- OLED --------

  updateOLED(inputVoltage, current, temperatureC, SOC,

             dischargedAh, alarmActive);



  // -------- SERIAL MONITOR --------

  Serial.print("Tegangan: ");

  Serial.print(inputVoltage, 2);

  Serial.print(" V | Arus: ");

  Serial.print(current, 2);

  Serial.print(" A | Suhu: ");

  if (isfinite(temperatureC)) Serial.print(temperatureC, 1);

  else Serial.print("INVALID");

  Serial.print(" C | SOC: ");

  Serial.print(SOC, 1);

  Serial.print("% | Ah keluar: ");

  Serial.print(dischargedAh, 3);

  Serial.print(" | dT/dt: ");

  Serial.print(temperatureRiseRate, 2);

  Serial.print(" C/s | Status: ");
  Serial.println(alarmActive ? "FAULT" : "NORMAL");

  // -------- MQTT TELEMETRY PUBLISH --------
  if (now - lastMqttSendMs >= MQTT_INTERVAL_MS) {
    lastMqttSendMs = now;
    if (mqttClient.connected()) {
      char payload[180];
      float tempVal = isfinite(temperatureC) ? temperatureC : 0.0f;
      float socVal = isfinite(SOC) ? SOC : 0.0f;
      snprintf(payload, sizeof(payload),
        "{\"v\":%.2f,\"i\":%.2f,\"t\":%.1f,\"soc\":%.1f,\"ah\":%.3f,\"relay\":%d,\"fault\":%d}",
        inputVoltage, current, tempVal, socVal, dischargedAh, !alarmActive ? 1 : 0, alarmActive ? 1 : 0);
      mqttClient.publish(MQTT_TOPIC, payload);
    }
  }

  delay(300);
}
