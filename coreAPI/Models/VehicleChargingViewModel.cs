using System;
using System.ComponentModel.DataAnnotations;

namespace coreAPI.Models
{
    public class VehicleChargingViewModel
    {
        // Sección Programming
        [Display(Name = "Mode")]
        public string Mode { get; set; } = "Current";  // "Current" o "kWh"

        [Display(Name = "Current (A)")]
        public int Current { get; set; }

        [Display(Name = "SOC Desired (%)")]
        public int SOCDesired { get; set; }

        [Display(Name = "Departure Time")]
        public string DepartureTime { get; set; } = "08:30";

        // Sección Charge on Cheaper Hours
        [Display(Name = "Charge On Cheaper Hours")]
        public bool ChargeOnCheaperHours { get; set; }

        // Sección Charging Plan (solo lectura)
        [Display(Name = "Start")]
        public DateTime ChargingPlanStart { get; set; }

        [Display(Name = "End")]
        public DateTime ChargingPlanEnd { get; set; }

        [Display(Name = "Time")]
        public double ChargingPlanTime { get; set; }

        [Display(Name = "From")]
        public int FromPercentage { get; set; }

        [Display(Name = "To")]
        public int ToPercentage { get; set; }

        [Display(Name = "To Add")]
        public int ToAddPercentage { get; set; }

        [Display(Name = "Add (kWh)")]
        public double Add { get; set; }

        [Display(Name = "Used (kWh)")]
        public double Used { get; set; }

        [Display(Name = "Output (kWh)")]
        public double Output { get; set; }

        [Display(Name = "Intake (kWh)")]
        public double Intake { get; set; }

        [Display(Name = "Charge (km/h)")]
        public double Charge { get; set; }

        [Display(Name = "Range Added (km)")]
        public double RangeAdded { get; set; }

        [Display(Name = "Battery Capacity (kWh)")]
        public double BatteryCapacity { get; set; }

        // Sección Actual Schedule
        [Display(Name = "Actual Start")]
        public DateTime ActualStart { get; set; }

        [Display(Name = "Actual End")]
        public DateTime ActualEnd { get; set; }

        [Display(Name = "Actual Time")]
        public string ActualTime { get; set; } = "00:00";
    }
}
