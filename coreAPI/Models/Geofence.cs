namespace coreAPI.Models
{
    public partial class Geofence
    {
        public Geofence()
        {
            ChargingProcesses = new HashSet<ChargingProcess>();
            DrifeEndGeofences = new HashSet<Drife>();
            DrifeStartGeofences = new HashSet<Drife>();
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
        public virtual ICollection<Drife> DrifeEndGeofences { get; set; }
        public virtual ICollection<Drife> DrifeStartGeofences { get; set; }
    }
}
