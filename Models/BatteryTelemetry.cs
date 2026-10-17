using System;

namespace DashboardMonitorBateraiManajemen.Models
{
    public class BatteryTelemetry
    {
        public double Voltage { get; set; }       // Volt (V)
        public double Current { get; set; }       // Ampere (A)
        public double Temperature { get; set; }   // Celsius (°C)
        public double SOC { get; set; }           // State of Charge (0 - 100%)
        public double CapacityAh { get; set; }    // Kapasitas / Deteksi Ah (Ampere-Hour)
        public double NominalCapacityAh { get; set; } = 100.0; // Kapasitas Nominal Pack (100 Ah)
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Computed Properties
        public double Power => Math.Round(Voltage * Current, 2); // Watt (W)
        public bool IsCharging => Current > 0.05;
        public bool IsDischarging => Current < -0.05;

        public double CapacityPercentage => NominalCapacityAh > 0 
            ? Math.Clamp(Math.Round((CapacityAh / NominalCapacityAh) * 100.0, 1), 0, 100) 
            : 0;

        public string OperatingMode
        {
            get
            {
                if (IsCharging) return "CHARGING";
                if (IsDischarging) return "DISCHARGING";
                return "STANDBY";
            }
        }

        public string CapacityStatus
        {
            get
            {
                if (CapacityAh >= 80) return "OPTIMAL (> 80 Ah)";
                if (CapacityAh >= 40) return "NORMAL (40-80 Ah)";
                if (CapacityAh >= 20) return "LOW CAPACITY (< 40 Ah)";
                return "CRITICAL EMPTY";
            }
        }

        public string ThermalStatus
        {
            get
            {
                if (Temperature < 15) return "COLD";
                if (Temperature <= 45) return "NORMAL";
                if (Temperature <= 60) return "WARM";
                return "OVERHEAT ALERT";
            }
        }
    }
}
