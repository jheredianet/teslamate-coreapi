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
        public int Amp { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public double From { get; set; }
        public double To { get; set; }
        public double ToAdd { get; set; }
        public double Added { get; set; }
        public double Used { get; set; }
        public double Output { get; set; }
        public double Intake { get; set; }
        public double Charge { get; set; }
        public double RangeAdded { get; set; }
        public double BatteryCapacity { get; set; }
        public double Time { get; set; }
        public OpenEVSE_Schedule? Schedule { get; set; } = null!;
    }

    public class OpenEVSE_Schedule()
    {
        public DateTime StartTime { get; set; }
        public DateTime DepartureTime { get; set; }

        public string DurationTime
        {
            get
            {
                TimeSpan duration = DepartureTime - StartTime;
                double totalMinutes = duration.TotalMinutes;
                int hours = (int)(totalMinutes / 60);
                int minutes = (int)(totalMinutes % 60);
                string formattedTime = $"{hours:D2}:{minutes:D2}h";

                return formattedTime;

            }
        }
    }
}
