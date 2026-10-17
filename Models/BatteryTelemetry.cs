using System;

namespace DashboardMonitorBateraiManajemen.Models
{
    public class BatteryTelemetry
    {
        public double Voltage { get; set; }       // Volt (V) - Nominal 12.8V LiFePO4 (10.0V - 14.6V)
        public double Current { get; set; }       // Ampere (A) - ACS712 (-30A s/d +30A)
        public double Temperature { get; set; }   // Celsius (°C) - NTC 10K
        public double SOC { get; set; }           // State of Charge (0 - 100%)
        public double CapacityAh { get; set; }    // Deteksi Ah (Ah keluar / Coulomb counting)
        public double NominalCapacityAh { get; set; } = 100.0; // Kapasitas Nominal Pack (100 Ah)
        public bool RelayActive { get; set; } = true;          // Status Relay (True = ON, False = Cutoff)
        public bool IsFault { get; set; } = false;             // Status Alarm Proteksi (True = Fault)
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Computed Properties
        public double Power => Math.Round(Voltage * Current, 2); // Watt (W)
        public bool IsDischarging => Current > 0.10;
        public bool IsCharging => Current < -0.10;

        public double CapacityPercentage => NominalCapacityAh > 0 
            ? Math.Clamp(Math.Round((CapacityAh / NominalCapacityAh) * 100.0, 1), 0, 100) 
            : 0;

        public string OperatingMode
        {
            get
            {
                if (IsDischarging) return "DISCHARGING (+)";
                if (IsCharging) return "CHARGING (-)";
                return "STANDBY (0.0 A)";
            }
        }

        public string RelayStatusText => RelayActive ? "RELAY: AKTIF (ON)" : "RELAY: CUT-OFF (OFF)";
        public string FaultStatusText => IsFault ? "ALARM FAULT!" : "SISTEM NORMAL";

        public string CapacityStatus
        {
            get
            {
                if (CapacityAh >= 80) return "TINGGI (> 80 Ah)";
                if (CapacityAh >= 40) return "SEDANG (40-80 Ah)";
                if (CapacityAh > 0) return "RENDAH (< 40 Ah)";
                return "0 Ah TERPAKAI";
            }
        }

        public string ThermalStatus
        {
            get
            {
                if (Temperature < 15) return "DINGIN";
                if (Temperature <= 45) return "NORMAL";
                if (Temperature <= 60) return "HANGAT";
                return "THERMAL FAULT!";
            }
        }
    }
}
