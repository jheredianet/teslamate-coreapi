using coreAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
            DateTime? LastEndDate = new DateTime();
            var lastReg = _context.OnlyDrives.OrderByDescending(c => c.EndDate).Take(1).SingleOrDefault();
            if (lastReg != null) LastEndDate = lastReg.EndDate;

            #region 'Old Code'
            //var car_efficiency = (from car in _context.Cars where car.Id == 1 select car.Efficiency).Single();
            //var DriveData = (
            //                from d in _context.Drives
            //                where d.StartDate >= LastEndDate && d.EndDate != null
            //                select new
            //                {
            //                    idTemp = d.Id,
            //                    start_date = d.StartDate,
            //                    end_date = d.EndDate,
            //                    drive = true,
            //                    distance = d.Distance,
            //                    duration_min = d.DurationMin,
            //                    Temperature = d.OutsideTempAvg,
            //                    StartRatedRangeKm = Convert.ToDouble(d.StartRatedRangeKm)
            //                    //EndRatedRangeKm = Convert.ToDouble(d.EndRatedRangeKm)
            //                    //efficiency = d.StartRatedRangeKm.HasValue && d.EndRatedRangeKm.HasValue && d.Distance.HasValue && (d.EndRatedRangeKm - d.StartRatedRangeKm) > 0 ? d.Distance / Convert.ToDouble(d.EndRatedRangeKm - d.StartRatedRangeKm) : null   //Convert.ToDecimal(d.Distance.ToString()) / (Convert.ToDecimal(d.EndRatedRangeKm.ToString()) - Convert.ToDecimal(d.StartRatedRangeKm.ToString())),
            //                    //consumption_kWh = Convert.ToDecimal(0) //Convert.ToDecimal(Convert.ToDecimal(d.EndRatedRangeKm.ToString()) - Convert.ToDecimal(d.StartRatedRangeKm.ToString())) * Convert.ToDecimal(car_efficiency)
            //                }
            //                )
            //        .Concat(
            //                from c in _context.ChargingProcesses
            //                where c.StartDate >= LastEndDate && c.EndDate != null
            //                select new
            //                {
            //                    idTemp = c.Id,
            //                    start_date = c.StartDate,
            //                    end_date = c.EndDate,
            //                    drive = false,
            //                    distance = (double?)0,
            //                    duration_min = (short?)0,
            //                    Temperature = c.OutsideTempAvg,
            //                    StartRatedRangeKm = Convert.ToDouble(0),
            //                    //EndRatedRangeKm = 0.0
            //                    //efficiency = (double?)0 //Convert.ToDecimal(0.0),
            //                    //consumption_kWh = 0.0
            //                }
            //                )
            //        .OrderBy(x => x.start_date);
            #endregion

            // Get Data from view
            //var DriveData = from d in _context.DriveDataViews  where d.StartDate >= LastEndDate && d.EndDate != null select d ;
            var DriveData = from d in _context.DriveDataViews where d.StartDate >= LastEndDate && d.EndDate >= DateTime.MinValue select d;
            var drive = new OnlyDrive();
            var counter = 0;
            var driveproccesed = 0;
            foreach (var i in DriveData)
            {
                if (i.drive == 1) // Is drive
                {
                    if (drive.StartDate.Year < 1900)
                    { // Just the first time take data from related tables
                        drive.StartDate = i.StartDate;
                        drive.StartBatteryLevel = i.StartBatteryLevel;
                    }
                    drive.Distance += i.Distance;
                    drive.DurationMin = Convert.ToInt16(drive.DurationMin + i.DurationMin);
                    drive.EndDate = i.EndDate;
                    drive.EndBatteryLevel = i.EndBatteryLevel;
                    drive.Temperature += i.Temperature;
                    drive.SpeedAvg += i.SpeedAvg;
                    drive.PowerMax = i.PowerMax > drive.PowerMax ? i.PowerMax : drive.PowerMax;
                    drive.Consumption_kWh += i.Consumption_kWh;
                    drive.Consumption_kWh_km += i.Consumption_kWh_km;
                    drive.Efficiency += i.Efficiency;
                    driveproccesed += 1;
                }
                else // Is Charge, reset
                {
                    // if (drive.StartDate.Year >= 1900 && drive.EndDate != null)
                    if (drive.StartDate.Year >= 1900 && drive.EndDate >= DateTime.MinValue)
                    {
                        // Calculate means
                        drive.Temperature /= driveproccesed;
                        drive.SpeedAvg /= driveproccesed;
                        drive.PowerMax /= driveproccesed;
                        drive.Consumption_kWh_km /= driveproccesed;
                        drive.Efficiency /= driveproccesed;

                        _context.OnlyDrives.Add(drive);

                        drive = new OnlyDrive();
                        driveproccesed = 0;

                        // Drives merged
                        counter += 1;
                    }
                }
            }
            _context.SaveChanges();
            return counter;
        }
    }
}
