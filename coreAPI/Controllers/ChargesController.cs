using coreAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace coreAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ChargesController : ControllerBase
    {
        private readonly teslamateContext _context;

        public ChargesController(teslamateContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Route("GetVersion")]
        public async Task<ActionResult<Version>> GetVersion()
        {
            await Task.Delay(0);
            var Version = typeof(ChargesController).Assembly.GetName().Version;
            return Ok(Version);
        }

        [HttpGet]
        [Route("GetCharges")]
        public async Task<ActionResult<ChargingProcess>> GetCharges()
        {
            //return Ok(await _context.ChargingProcesses.ToListAsync());
            var lastFiveCharges = await _context.ChargingProcesses.OrderByDescending(c => c.EndDate).Take(5).ToListAsync();
            return Ok(lastFiveCharges);
        }

        [HttpGet]
        [Route("ProcessDrives")]
        public async Task<ActionResult<int>> ProcessDrives()
        {
            try
            {
                var recProccesed = await Task.Run(() => processDrivesOnDatabase());
                return Ok(recProccesed);
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += string.Format(" - {0}", ex.InnerException.Message);
                return BadRequest(msg);
            }

        }

        [HttpPost]
        [Route("Union")]
        public async Task<ActionResult<ChargingProcess>> Union(int SourceID, int TargetID)
        {

            try
            {
                if ((TargetID - SourceID) != 1) throw new Exception("TargetID must be greater than SourceID");
                var Records = await Task.Run(() => MixCharges(SourceID, TargetID));
                return Ok(Records);
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += string.Format(" - {0}", ex.InnerException.Message);
                return BadRequest(msg);
            }
        }

        private List<ChargingProcess> MixCharges(int SourceID, int TargetID)
        {
            var sourceCharging = _context.ChargingProcesses.SingleOrDefault(c => c.Id == SourceID);
            var targetCharging = _context.ChargingProcesses.SingleOrDefault(c => c.Id == TargetID);

            if (sourceCharging == null) throw new Exception("SourceID not found");
            if (targetCharging == null) throw new Exception("TargetID not found");

            // Update Target
            targetCharging.StartDate = sourceCharging.StartDate;
            targetCharging.StartIdealRangeKm = sourceCharging.StartIdealRangeKm;
            targetCharging.StartRatedRangeKm = sourceCharging.StartRatedRangeKm;
            targetCharging.StartBatteryLevel = sourceCharging.StartBatteryLevel;
            targetCharging.Cost += sourceCharging.Cost;
            targetCharging.ChargeEnergyAdded += sourceCharging.ChargeEnergyAdded;
            targetCharging.ChargeEnergyUsed += sourceCharging.ChargeEnergyUsed;
            targetCharging.DurationMin = (short?)(targetCharging.DurationMin + sourceCharging.DurationMin);

            // Delete Record
            _context.ChargingProcesses.Remove(sourceCharging);

            // Update Charge Process
            var Charges = _context.Charges.Where(c => c.ChargingProcessId == SourceID).ToList();
            Charges.ForEach(c => c.ChargingProcessId = TargetID);

            // Save Changes
            _context.SaveChanges();

            var Records = new List<ChargingProcess>();
            Records.Add(sourceCharging);
            Records.Add(targetCharging);

            return Records;
        }

        private int processDrivesOnDatabase()
        {
            // var carID = 1;
            var counter = 0;

            // This stays just as an explample. It's directly replaced by Continues Trips Dashboard

            /*
            DateTime? LastEndDate = new DateTime();
            var lastReg = _context.OnlyDrives.OrderByDescending(c => c.EndDate).Take(1).SingleOrDefault();
            if (lastReg != null) LastEndDate = lastReg.EndDate;

            // Get Data
            var Efficiency = (from c in _context.Cars where c.Id == carID select c.Efficiency).Single();
            var lastCompleteCharges = (from cp in _context.ChargingProcesses where cp.CarId == carID && cp.EndDate >= LastEndDate orderby cp.StartDate select cp.EndDate).ToList();

            // Now from the dates let's get the drives
            for (int i = 0; i < lastCompleteCharges.Count() - 1; i++) // Just process when there are at leat two charges
            {
                var fromDate = lastCompleteCharges[i];
                var toDate = lastCompleteCharges[i + 1];

                // Get Data from View
                var DriveData = from d in _context.Drives
                                where (d.StartDate >= fromDate && d.EndDate <= toDate) &&
                                    d.Distance > 1 && d.DurationMin > 0 
                                orderby d.StartDate
                                select d;
                var Regs = DriveData.Count();

                if (Regs > 0)
                {
                    var Positions = from p in _context.Positions where p.DriveId >= DriveData.First().Id && p.DriveId <= DriveData.Last().Id orderby p.Date select p.UsableBatteryLevel;
                    var drive = new OnlyDrive();
                    drive.StartDate = DriveData.First().StartDate;
                    drive.StartBatteryLevel = Convert.ToInt16(Positions.First());
                    drive.EndBatteryLevel = Convert.ToInt16(Positions.Last());
                    drive.Distance = Convert.ToDouble(DriveData.Last().EndKm - DriveData.First().StartKm); // Convert.ToDouble(DriveData.Sum(d => d.Distance));
                    drive.DurationMin = Convert.ToInt16(DriveData.Sum(d => d.DurationMin));
                    drive.EndDate = Convert.ToDateTime(DriveData.Last().EndDate);
                    drive.Temperature = Convert.ToDecimal(DriveData.Average(d => d.OutsideTempAvg));
                    drive.SpeedAvg = Convert.ToDecimal(drive.Distance / drive.DurationMin * 60);
                    drive.PowerMax = Convert.ToInt16(DriveData.Max(d => d.PowerMax));
                    var Range_diff = Convert.ToDouble(DriveData.First().StartRatedRangeKm - DriveData.Last().EndRatedRangeKm);
                    drive.Consumption_kWh = Convert.ToDouble(Range_diff * Efficiency);
                    drive.Consumption_kWh_km = drive.Consumption_kWh / drive.Distance * 1000;
                    drive.Efficiency = drive.Distance / Range_diff;
                    _context.OnlyDrives.Add(drive);
                    counter += Regs;
                    _context.SaveChanges();
                }
            }
            */

            return counter;
        }
    }
}
