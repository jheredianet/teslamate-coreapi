namespace coreAPI.Models
{
    public partial class Geofence
    {
        public Geofence()
        {
            ChargingProcesses = new HashSet<ChargingProcess>();
            DrifeEndGeofences = new HashSet<Drive>();
            DrifeStartGeofences = new HashSet<Drive>();
        }

        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public short Radius { get; set; }
        public DateTime InsertedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public decimal? CostPerUnit { get; set; }
        public decimal? SessionFee { get; set; }

        public virtual ICollection<ChargingProcess> ChargingProcesses { get; set; }
        public virtual ICollection<Drive> DrifeEndGeofences { get; set; }
        public virtual ICollection<Drive> DrifeStartGeofences { get; set; }
    }
}
