using System;

namespace DashboardMonitorBateraiManajemen.Models
{
    public class BatteryTelemetry
    {
        public double Voltage { get; set; }       // Volt (V)
        public double Current { get; set; }       // Ampere (A)
        public double Temperature { get; set; }   // Celsius (°C)
        public double SOC { get; set; }           // State of Charge (0 - 100%)
        public double SOH { get; set; }           // State of Health (0 - 100%)
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Computed Properties
        public double Power => Math.Round(Voltage * Current, 2); // Watt (W)
        public bool IsCharging => Current > 0.05;
        public bool IsDischarging => Current < -0.05;

        public string OperatingMode
        {
            get
            {
                if (IsCharging) return "CHARGING";
                if (IsDischarging) return "DISCHARGING";
                return "STANDBY";
            }
        }

        public string HealthGrade
        {
            get
            {
                if (SOH >= 95) return "GRADE A+ (OPTIMAL)";
                if (SOH >= 85) return "GRADE A (GOOD)";
                if (SOH >= 75) return "GRADE B (FAIR)";
                return "GRADE C (DEGRADED)";
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
