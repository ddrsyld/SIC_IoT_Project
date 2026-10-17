/*
 * =========================================================================================
 * BATERAI MANAGEMENT SYSTEM (BMS) TELEMETRY SIMULATOR VIA MQTT UNTUK WOKWI (ESP32)
 * =========================================================================================
 * Sketch ini menyimulasikan pembacaan sensor baterai dan mengirimkannya via MQTT (Internet)
 * serta Serial Monitor secara berkala.
 * 
 * Parameter Telemetri:
 * 1. Tegangan (Voltage, V)
 * 2. Arus (Current, A)
 * 3. Suhu (Temperature, °C)
 * 4. SOC (State of Charge, %)
 * 5. Deteksi Kapasitas (Capacity, Ah)  -> [SOH telah digantikan dengan Deteksi Ah]
 *
 * Koneksi Jaringan:
 * - WiFi SSID : Wokwi-GUEST (Virtual WiFi bawaan Wokwi, tanpa password)
 * - MQTT Broker: broker.emqx.io (Port: 1883)
 * - MQTT Topic : sic/bms/telemetry
 * =========================================================================================
 */

#include <WiFi.h>
#include <PubSubClient.h>

// 1. Konfigurasi WiFi Wokwi
const char* ssid = "Wokwi-GUEST";
const char* password = "";

// 2. Konfigurasi MQTT Broker Publik
const char* mqtt_server = "broker.emqx.io";
const int mqtt_port = 1883;
const char* mqtt_topic = "sic/bms/telemetry";

WiFiClient espClient;
PubSubClient client(espClient);

// 3. Parameter Awal Baterai
float voltage = 48.60;          // Volt (Pack 48V / 16S)
float currentVal = 14.50;       // Ampere (+ = Charging, - = Discharging)
float temperature = 31.2;       // Derajat Celsius
float nominalCapacity = 100.0;  // Kapasitas Nominal Pack (100 Ah)
float capacityAh = 86.0;        // Deteksi Kapasitas Terisi (Ah)
float soc = 86.0;               // State of Charge (%)

unsigned long lastSendTime = 0;
const unsigned long sendInterval = 500; // Kirim tiap 500 ms (2 Hz agar stabil di MQTT publik)

void setup_wifi() {
  delay(10);
  Serial.println();
  Serial.print("[WiFi] Menghubungkan ke ");
  Serial.println(ssid);

  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid, password);

  while (WiFi.status() != WL_CONNECTED) {
    delay(300);
    Serial.print(".");
  }

  Serial.println();
  Serial.println("[WiFi] Terhubung!");
  Serial.print("[WiFi] IP Address: ");
  Serial.println(WiFi.localIP());
}

void reconnect_mqtt() {
  while (!client.connected()) {
    Serial.print("[MQTT] Menghubungkan ke broker: ");
    Serial.print(mqtt_server);
    Serial.print("...");

    String clientId = "ESP32_BMS_Wokwi_" + String(random(0xffff), HEX);

    if (client.connect(clientId.c_str())) {
      Serial.println(" Berhasil terhubung!");
      Serial.print("[MQTT] Siap publish ke topic: ");
      Serial.println(mqtt_topic);
    } else {
      Serial.print(" Gagal, rc=");
      Serial.print(client.state());
      Serial.println(" (Coba lagi dalam 2 detik)");
      delay(2000);
    }
  }
}

void setup() {
  Serial.begin(115200);
  randomSeed(analogRead(0));

  Serial.println("\n==========================================");
  Serial.println("  BMS TELEMETRY ESP32 SIMULATION (WOKWI) ");
  Serial.println("==========================================");

  setup_wifi();
  client.setServer(mqtt_server, mqtt_port);
}

void loop() {
  // Pastikan koneksi WiFi & MQTT tetap aktif
  if (WiFi.status() != WL_CONNECTED) {
    setup_wifi();
  }
  if (!client.connected()) {
    reconnect_mqtt();
  }
  client.loop();

  unsigned long currentTime = millis();
  if (currentTime - lastSendTime >= sendInterval) {
    lastSendTime = currentTime;

    // 1. Simulasikan fluktuasi sensor baterai secara dinamis
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

    // 2. Deteksi Kapasitas (Ah) & SOC bergerak mengikuti arus
    // Rumus Coulomb Counting sederhana: Ah = Ah + (I * dt)
    capacityAh += (currentVal * 0.002);
    if (capacityAh < 5.0) capacityAh = 5.0;
    if (capacityAh > nominalCapacity) capacityAh = nominalCapacity;

    soc = (capacityAh / nominalCapacity) * 100.0;
    if (soc < 5.0) soc = 5.0;
    if (soc > 100.0) soc = 100.0;

    // 3. Format Payload JSON (Tanpa SOH, digantikan dengan Ah)
    // Contoh: {"v":48.60,"i":14.50,"t":31.2,"soc":86.0,"ah":86.0}
    char payload[128];
    snprintf(payload, sizeof(payload),
             "{\"v\":%.2f,\"i\":%.2f,\"t\":%.1f,\"soc\":%.1f,\"ah\":%.1f}",
             voltage, currentVal, temperature, soc, capacityAh);

    // 4. Publish ke MQTT Broker
    bool published = client.publish(mqtt_topic, payload);

    // 5. Cetak juga ke Serial Monitor Wokwi
    if (published) {
      Serial.print("[MQTT PUBLISH] ");
    } else {
      Serial.print("[MQTT FAIL] ");
    }
    Serial.println(payload);
  }
}
