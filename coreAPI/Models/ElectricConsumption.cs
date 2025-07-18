namespace coreAPI.Models
{
    public class ElectricConsumption
    {
        public DateTime FechaHora { get; set; }
        public double Consumo { get; set; }
        public double? P1 { get; set; }
        public double? P2 { get; set; }
        public double? P3 { get; set; }


        public bool RealLecture { get; set; }
    }
}
