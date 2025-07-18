﻿using coreAPI.Models;
using ExcelDataReader;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Npgsql;
using Renci.SshNet;
using System.Xml.Linq;


namespace coreAPI.Classes
{
    public class ToolsData
    {
        private readonly teslamateContext db = new teslamateContext();
        private readonly InfluxDbConnection influxDbConnection;
        private readonly AppSettings appSettings;

        private IQueryable<ChargingProcess> GetChargingProcess()
        {
            return db.ChargingProcesses
                .Where(c => c.ChargeEnergyAdded > 0 && c.Cost == null && c.EndDate != null); ;
        }

        public ToolsData(teslamateContext context)
        {
            db = context;
            influxDbConnection = context.GetService<InfluxDbConnection>();
            appSettings = context.GetService<AppSettings>();
        }

        public async Task<List<ChargingProcess>> ListChargingProcessWithoutCost()
        {
            // Charges with cost null  and added some kWh
            // SELECT * FROM charging_processes WHERE charge_energy_added > 0 and cost is NULL 

            return await GetChargingProcess()
                .OrderByDescending(c => c.EndDate).ToListAsync();
            //return await db.ChargingProcesses.OrderByDescending(c => c.EndDate).Take(5).ToListAsync();
        }

        public async Task<List<ChargingProcess>> ProcessChargesAtHomeWithoutCost()
        {
            // Get charges from PostgreSQL
            var charges = GetChargingProcess().Where(c => c.GeofenceId == 1).OrderByDescending(c => c.EndDate);

            // Connect to InfluxDB
            using var client = new InfluxDBClient(influxDbConnection.Url, influxDbConnection.Token);

            foreach (var charge in charges)
            {
                var start = charge.StartDate.ToString("yyyy-MM-ddTHH:mm:ssZ");
                var end = charge.EndDate?.ToString("yyyy-MM-ddTHH:mm:ssZ");

                //var flux = string.Format("from(bucket: \"{0}\")", influxDbConnection.Bucket) +
                //    string.Format(" |> range(start: {0}, stop: {1}) ", start, end) +
                //    " |> filter(fn: (r) => r._measurement == \"OpenEVSEConsumption\" and (r._field == \"Cost\" or r._field == \"Consumption\" or r._field == \"CustomCost\"))" +
                //    " |> pivot(rowKey:[\"_time\"], columnKey: [\"_field\"], valueColumn: \"_value\")" +
                //    " |> map(fn: (r) => ({ r with _value: r.Consumption * r.Cost }))" +
                //    " |> sum()";

                // CustomCost - Octopus
                var flux = string.Format("from(bucket: \"{0}\")", influxDbConnection.Bucket) +
                    string.Format(" |> range(start: {0}, stop: {1}) ", start, end) +
                    " |> filter(fn: (r) => r._measurement == \"OpenEVSEConsumption\" and (r._field == \"Cost\" or r._field == \"Consumption\" or r._field == \"CustomCost\"))" +
                    " |> pivot(rowKey:[\"_time\"], columnKey: [\"_field\"], valueColumn: \"_value\")" +
                    " |> map(fn: (r) => ({ r with _value: r.Consumption * r.CustomCost }))" +
                    " |> sum()";

                var fluxTables = await client.GetQueryApi().QueryAsync(flux, influxDbConnection.Organization);
                fluxTables.ForEach(fluxTable =>
                {
                    var fluxRecords = fluxTable.Records;
                    fluxRecords.ForEach(fluxRecord =>
                    {
                        //var t = fluxRecord.GetTime();
                        // Update PostgreSQL record
                        charge.Cost = Convert.ToDecimal(fluxRecord.GetValue());
                    });
                });
            }
            var updatesCharges = await charges.ToListAsync();
            db.SaveChanges();
            return updatesCharges;
        }

        public async Task<List<Drive>> IncompleteDrives()
        {
            return await db.Drives.Where(d => d.EndDate == null).OrderBy(d => d.StartDate).ToListAsync();
        }

        public async Task<List<ChargingProcess>> IncompleteCharges()
        {
            return await db.ChargingProcesses.Where(d => d.EndDate == null).OrderBy(d => d.StartDate).ToListAsync();
        }

        public async Task<List<Address>> SearchAddress(string IncludedName)
        {
            if (string.IsNullOrEmpty(IncludedName))
            {
                throw new ArgumentException("IncludedName cannot be null or empty.", nameof(IncludedName));
            }

            var Addresses = await db.Addresses
                .Where(a => a.Name != null && a.Name.ToLower().Contains(IncludedName.ToLower()))
                .ToListAsync();

            return Addresses;
        }

        public async Task<IncompleteData> IncompleteData()
        {
            var Incompletes = new IncompleteData();
            Incompletes.Drives = await IncompleteDrives();
            Incompletes.Charges = await IncompleteCharges();

            return Incompletes;
        }

        public async Task<int> ImportExcelFromUFD()
        {
            var Consumos = new List<ElectricConsumption>();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var ImportPath = appSettings.ImportPath;
            var ImportFile = Path.Combine(ImportPath, "consumptions.xlsx");
            if (System.IO.File.Exists(ImportFile))
            {
                using (var stream = System.IO.File.Open(ImportFile, FileMode.Open, FileAccess.Read))
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        do
                        {
                            while (reader.Read()) //Each ROW
                            {
                                // Skip first row
                                if (reader.GetString(1) == "Fecha") continue;

                                var c = new ElectricConsumption();
                                string t = reader.GetValue(1).ToString() ?? "";
                                string[] dateGroups = t.Split('/');
                                var h = (Convert.ToInt32(reader.GetValue(2)) - 1).ToString() ?? "";
                                string formattedNumber = h.PadLeft(2, '0');
                                string timeFormat = $"{formattedNumber}:00:00";

                                c.FechaHora = Convert.ToDateTime(string.Format("{0}-{1}-{2} {3}", dateGroups[2], dateGroups[1], dateGroups[0], timeFormat));
                                c.Consumo = Convert.ToDouble((reader.GetValue(3).ToString() ?? "").Replace(',', '.'));

                                if (double.TryParse((reader.GetValue(4)?.ToString() ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p1))
                                    c.P1 = p1;
                                if (double.TryParse((reader.GetValue(5)?.ToString() ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p2))
                                    c.P2 = p2;
                                if (double.TryParse((reader.GetValue(6)?.ToString() ?? "").Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p3))
                                    c.P3 = p3;

                                c.RealLecture = (reader.GetValue(7).ToString() ?? "") == "Real";
                                Consumos.Add(c);
                            }
                        } while (reader.NextResult()); //Move to NEXT SHEET
                    }
                }

                // Save Data to FluxDB
                using var influxDBClient = new InfluxDBClient(influxDbConnection.Url, influxDbConnection.Token);
                using (var writeApi = influxDBClient.GetWriteApi())
                {
                    foreach (var c in Consumos)
                    {
                        var point = PointData.Measurement("ElectricConsumption")
                        .Tag("location", "Home")
                        .Field("consumption", c.Consumo)
                        .Field("reallecture", c.RealLecture ? 1 : 0)
                        .Timestamp(c.FechaHora, WritePrecision.Ms);

                        writeApi.WritePoint(point, influxDbConnection.Bucket, influxDbConnection.Organization);
                    }

                }

                // Rename Procesed File
                if (Consumos.Count > 0)
                {

                    var minDate = Consumos.Min(c => c.FechaHora).ToString("yyyyMMdd");
                    var maxDate = Consumos.Max(c => c.FechaHora).ToString("yyyyMMdd");
                    var newFileName = string.Format("consumptions_{0}-{1}.xlsx", minDate, maxDate);
                    System.IO.File.Move(ImportFile, Path.Combine(ImportPath, newFileName));
                }
            }
            else
            {
                throw new FileNotFoundException(string.Format("File not found: {0} - App base Path {1}", ImportFile, appSettings.CurrentPath));
            }
            return await Task.FromResult(Consumos.Count);
        }

        public List<ShortTimeDrive> ShortTimeBetweenDrives(int Minutes)
        {
            var drives = new List<ShortTimeDrive>();
            using (var context = new teslamateContext())
            {
                using (var conn = new NpgsqlConnection(db.Database.GetConnectionString()))
                {
                    string senSQL = "WITH d AS(" +
                        "SELECT LAG(id) OVER(ORDER BY end_date) as prev_id, id, start_date, end_date, " +
                            "extract(epoch from age(start_date, LAG(end_date) OVER(ORDER BY end_date))) / 60 diff_min  FROM drives)" +
                        "SELECT * FROM d WHERE diff_min <= @mins";

                    conn.Open();

                    using (var cmd = new NpgsqlCommand(senSQL, conn))
                    {
                        cmd.Parameters.AddWithValue("@mins", Minutes);

                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var sd = new ShortTimeDrive();
                                sd.PrevId = (int)reader[0];
                                sd.Id = (int)reader[1];
                                sd.StartDate = (DateTime)reader[2];
                                sd.EndDate = (DateTime)reader[3];
                                sd.DiffMinutes = (decimal)reader[4];
                                drives.Add(sd);
                            }
                        }
                    }
                }
            }
            return drives;
        }

        public List<Drive> JoinDrives(int SourceID, int TargetID)
        {
            var sourceDrive = db.Drives.SingleOrDefault(c => c.Id == SourceID);
            var targetDrive = db.Drives.SingleOrDefault(c => c.Id == TargetID);

            if (sourceDrive == null) throw new Exception("SourceID not found");
            if (targetDrive == null) throw new Exception("TargetID not found");

            // Objects to return
            var Records = new List<Drive>();
            Records.Add(sourceDrive);
            Records.Add(targetDrive);

            // Update Target
            targetDrive.StartDate = sourceDrive.StartDate;
            targetDrive.OutsideTempAvg = (sourceDrive.OutsideTempAvg + targetDrive.OutsideTempAvg) / 2;
            targetDrive.SpeedMax = Math.Max(Convert.ToInt16(sourceDrive.SpeedMax), Convert.ToInt16(targetDrive.SpeedMax));
            targetDrive.PowerMax = Math.Max(Convert.ToInt16(sourceDrive.PowerMax), Convert.ToInt16(targetDrive.PowerMax));
            targetDrive.PowerMin = Math.Min(Convert.ToInt16(sourceDrive.PowerMin), Convert.ToInt16(targetDrive.PowerMin));
            targetDrive.StartIdealRangeKm = sourceDrive.StartIdealRangeKm;
            targetDrive.StartKm = sourceDrive.StartKm;
            targetDrive.Distance += sourceDrive.Distance;
            targetDrive.DurationMin = (short?)(targetDrive.DurationMin + sourceDrive.DurationMin);
            targetDrive.InsideTempAvg = (sourceDrive.InsideTempAvg + targetDrive.InsideTempAvg) / 2;
            targetDrive.StartAddressId = sourceDrive.StartAddressId;
            targetDrive.StartRatedRangeKm = sourceDrive.StartRatedRangeKm;
            targetDrive.StartPositionId = sourceDrive.StartPositionId;
            targetDrive.StartGeofenceId = sourceDrive.StartGeofenceId;

            // Update Positions
            // var Positions = db.Positions.Where(p => p.DriveId == SourceID).ToList();
            // Positions.ForEach(p => p.DriveId = TargetID);
            // Improve performance
            var n = db.Database.ExecuteSqlRaw("UPDATE positions SET drive_id = {0} WHERE drive_id = {1}", TargetID, SourceID);
            Console.WriteLine(string.Format("Updated {0} records", n));

            // Delete Record
            db.Drives.Remove(sourceDrive);

            // Save Changes
            db.SaveChanges();

            return Records;
        }

        public Task<int> ProcessAndFixAddresses()
        {
            // Update neighbourhood
            var neighbourhood = db.Database.ExecuteSqlRaw("UPDATE addresses  SET neighbourhood = city WHERE neighbourhood is NULL");
            Console.WriteLine(string.Format("Updated {0} neighbourhoods", neighbourhood));

            var senSQL = "UPDATE addresses SET name = CASE " +
                "WHEN road IS NOT NULL THEN road " +
                "WHEN neighbourhood IS NOT NULL THEN neighbourhood " +
                "WHEN city IS NOT NULL THEN city " +
                "ELSE state END " +
                "WHERE name IS NULL";
            var names = db.Database.ExecuteSqlRaw(senSQL);
            Console.WriteLine(string.Format("Updated {0} addresse names", names));

            return Task.FromResult(neighbourhood + names);
        }

        public Task<int> FixOfflineStatus()
        {
            var senSQL = "UPDATE states SET state = 'asleep' WHERE car_id = 1 " +
                "AND end_date>= '20240101' AND state = 'offline' " +
                "and DATE_PART('hour', end_date - start_date) >= 1";
            var records = db.Database.ExecuteSqlRaw(senSQL);
            Console.WriteLine(string.Format("Updated {0} states", records));
            return Task.FromResult(records);
        }

        public Task<int> RenameAddressName(int id, string NewName)
        {
            var senSQL = string.Format("UPDATE addresses SET name = '{0}' WHERE id = {1}", NewName, id);
            var records = db.Database.ExecuteSqlRaw(senSQL);
            Console.WriteLine(string.Format("Updated {0} Addresses", records));
            return Task.FromResult(records);
        }

        public List<ChargingProcess> MixCharges(int SourceID, int TargetID)
        {
            var sourceCharging = db.ChargingProcesses.SingleOrDefault(c => c.Id == SourceID);
            var targetCharging = db.ChargingProcesses.SingleOrDefault(c => c.Id == TargetID);

            if (sourceCharging == null) throw new Exception("SourceID not found");
            if (targetCharging == null) throw new Exception("TargetID not found");

            // Objects to return
            var Records = new List<ChargingProcess>();
            Records.Add(sourceCharging);
            Records.Add(targetCharging);

            // Update Target
            targetCharging.StartDate = sourceCharging.StartDate;
            targetCharging.StartIdealRangeKm = sourceCharging.StartIdealRangeKm;
            targetCharging.StartRatedRangeKm = sourceCharging.StartRatedRangeKm;
            targetCharging.StartBatteryLevel = sourceCharging.StartBatteryLevel;
            targetCharging.Cost += sourceCharging.Cost;
            targetCharging.ChargeEnergyAdded += sourceCharging.ChargeEnergyAdded;
            targetCharging.ChargeEnergyUsed += sourceCharging.ChargeEnergyUsed;
            targetCharging.DurationMin = (short?)(targetCharging.DurationMin + sourceCharging.DurationMin);

            // Update Charge Process
            // var Charges = db.Charges.Where(c => c.ChargingProcessId == SourceID).ToList();
            // Charges.ForEach(c => c.ChargingProcessId = TargetID);
            // Improve performance
            var n = db.Database.ExecuteSqlRaw("UPDATE charges SET charging_process_id = {0} WHERE charging_process_id = {1}", TargetID, SourceID);
            Console.WriteLine(string.Format("Updated {0} records", n));

            // Delete Record
            db.ChargingProcesses.Remove(sourceCharging);

            // Save Changes
            db.SaveChanges();

            return Records;
        }
        public int processDrivesOnDatabase()
        {
            // var carID = 1;
            var counter = int.MaxValue;

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

        public int ProcessShortDrives(int Minutes)
        {
            int i = 0;
            var ShortDrives = ShortTimeBetweenDrives(Minutes);
            foreach (var drive in ShortDrives)
            {
                int sourceID = drive.PrevId;
                int targetID = drive.Id;
                Console.WriteLine("{2} - Join drive {0} into {1}", sourceID, targetID, DateTime.Now);
                JoinDrives(sourceID, targetID);
                i++;
            }
            return i;
        }

        public List<GpxWayPoint> XmltoWpt(string xml)
        {
            XDocument gpxDocument = XDocument.Parse(xml);
            XNamespace ns = "http://www.topografix.com/GPX/1/1";
            // Query the XML to extract wpt elements and their child elements
            var wptData = gpxDocument.Descendants(ns + "wpt").Select(wpt => new GpxWayPoint
            {
                Lat = wpt.Attribute("lat")?.Value,
                Lon = wpt.Attribute("lon")?.Value,
                Name = wpt.Element(ns + "name")?.Value,
                Desc = wpt.Element(ns + "desc")?.Value
            }).ToList();
            return wptData;
        }

        public string runSSHCommand(string command)
        {
            string MessageReturn = string.Empty;

            if (string.IsNullOrEmpty(appSettings.sshKeyFile) ||
            string.IsNullOrEmpty(appSettings.sshUserName) ||
            string.IsNullOrEmpty(appSettings.sshServer))
            {
                throw new Exception("No credencials found for accesing SSH server");
            }
            else
            {
                var keyFile = appSettings.sshKeyFile;
                if (!System.IO.File.Exists(keyFile))
                {
                    throw new Exception(string.Format("No key file {0}, for accesing SSH server found", keyFile));
                }

                // Load your private key file (replace with the actual path to your key file)
                var privateKey = new PrivateKeyFile(Path.Combine(appSettings.CurrentPath, appSettings.sshKeyFile));
                // Create a list of authentication methods (only private key in this case)
                var methods = new List<AuthenticationMethod> { new PrivateKeyAuthenticationMethod(appSettings.sshUserName, privateKey) };
                var connInfo = new Renci.SshNet.ConnectionInfo(appSettings.sshServer, appSettings.sshPort, appSettings.sshUserName, methods.ToArray());

                using (var sshClient = new SshClient(connInfo))
                {
                    sshClient.Connect();
                    var result = sshClient.RunCommand(command);
                    MessageReturn = (result.ExitStatus == 0) ? result.Result : result.Error;

                    sshClient.Disconnect();
                }
                return MessageReturn;
            }
        }

        public string getCommand(int id, string typeOfRecord)
        {
            // Validate typeOfRecord
            if (typeOfRecord != "Drive" && typeOfRecord != "Charge")
            {
                throw new Exception("Invalid typeOfRecord. Allowed values are 'Drive' or 'Charge'.");
            }
            string command;
            if (typeOfRecord == "Drive")
            {
                command = string.Format("docker exec teslamate bin/teslamate rpc \"TeslaMate.Repo.get!(TeslaMate.Log.Drive, {0}) |> TeslaMate.Log.close_drive()\"", id);
            }
            else
            {
                command = string.Format("docker exec teslamate bin/teslamate rpc \"TeslaMate.Repo.get!(TeslaMate.Log.ChargingProcess, {0}) |> TeslaMate.Log.complete_charging_process()\"", id);
            }
            return command;
        }
    }
}
