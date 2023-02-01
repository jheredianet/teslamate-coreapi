using System.Text.Json.Serialization;

namespace coreAPI.Models
{
    public partial class ChargingProcess
    {
        public ChargingProcess()
        {
            Charges = new HashSet<Charge>();
        }

        public int Id { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? ChargeEnergyAdded { get; set; }
        public decimal? StartIdealRangeKm { get; set; }
        public decimal? EndIdealRangeKm { get; set; }
        public short? StartBatteryLevel { get; set; }
        public short? EndBatteryLevel { get; set; }
        public short? DurationMin { get; set; }
        public decimal? OutsideTempAvg { get; set; }
        public short CarId { get; set; }
        public int PositionId { get; set; }
        public int? AddressId { get; set; }
        public decimal? StartRatedRangeKm { get; set; }
        public decimal? EndRatedRangeKm { get; set; }
        public int? GeofenceId { get; set; }
        public decimal? ChargeEnergyUsed { get; set; }
        public decimal? Cost { get; set; }
        [JsonIgnore]
        public virtual Address? Address { get; set; }
        [JsonIgnore]
        public virtual Car Car { get; set; } = null!;
        [JsonIgnore]
        public virtual Geofence? Geofence { get; set; }
        [JsonIgnore]
        public virtual Position Position { get; set; } = null!;
        [JsonIgnore]
        public virtual ICollection<Charge> Charges { get; set; }
    }
}
