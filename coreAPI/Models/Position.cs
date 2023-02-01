namespace coreAPI.Models
{
    public partial class Position
    {
        public Position()
        {
            ChargingProcesses = new HashSet<ChargingProcess>();
            DrifeEndPositions = new HashSet<Drife>();
            DrifeStartPositions = new HashSet<Drife>();
        }

        public int Id { get; set; }
        public DateTime Date { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public short? Speed { get; set; }
        public short? Power { get; set; }
        public double? Odometer { get; set; }
        public decimal? IdealBatteryRangeKm { get; set; }
        public short? BatteryLevel { get; set; }
        public decimal? OutsideTemp { get; set; }
        public short? Elevation { get; set; }
        public int? FanStatus { get; set; }
        public decimal? DriverTempSetting { get; set; }
        public decimal? PassengerTempSetting { get; set; }
        public bool? IsClimateOn { get; set; }
        public bool? IsRearDefrosterOn { get; set; }
        public bool? IsFrontDefrosterOn { get; set; }
        public short CarId { get; set; }
        public int? DriveId { get; set; }
        public decimal? InsideTemp { get; set; }
        public bool? BatteryHeater { get; set; }
        public bool? BatteryHeaterOn { get; set; }
        public bool? BatteryHeaterNoPower { get; set; }
        public decimal? EstBatteryRangeKm { get; set; }
        public decimal? RatedBatteryRangeKm { get; set; }
        public short? UsableBatteryLevel { get; set; }
        public decimal? TpmsPressureFl { get; set; }
        public decimal? TpmsPressureFr { get; set; }
        public decimal? TpmsPressureRl { get; set; }
        public decimal? TpmsPressureRr { get; set; }

        public virtual Car Car { get; set; } = null!;
        public virtual Drife? Drive { get; set; }
        public virtual ICollection<ChargingProcess> ChargingProcesses { get; set; }
        public virtual ICollection<Drife> DrifeEndPositions { get; set; }
        public virtual ICollection<Drife> DrifeStartPositions { get; set; }
    }
}
