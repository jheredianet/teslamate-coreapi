namespace coreAPI.Models
{
    public class InChargeProgramming
    {
        // Input
        public int Current { get; set; }
        public int TargetSOC { get; set; }
        public string DepartureTime { get; set; }

        public InChargeProgramming()
        {
            DepartureTime = "00:00";
        }
        public InChargeProgramming(int Current, int SOC, string DepartureHour)
        {
            this.Current = Current;
            this.TargetSOC = SOC;
            this.DepartureTime = DepartureHour;
        }

    }

    public class OutChargeProgramming : InChargeProgramming
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public int From { get; set; }
        public int To { get; set; }
        public double ToAdd { get; set; }
        public double Added { get; set; }
        public double Used { get; set; }
        public double Output { get; set; }
        public double Intake { get; set; }
        public double Charge { get; set; }
        public double RangeAdded { get; set; }
        public double BatteryCapacity { get; set; }
        public double Time { get; set; }
    }
}
