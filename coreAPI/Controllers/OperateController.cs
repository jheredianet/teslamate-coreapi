using coreAPI.Classes;
using coreAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Text;
using System.Xml.Linq;

namespace coreAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OperateController : ControllerBase
    {
        private readonly teslamateContext _context;
        private readonly ToolsData Tools;
        private readonly AppSettings appSettings;

        public OperateController(teslamateContext context)
        {
            _context = context;
            Tools = new ToolsData(_context);
            appSettings = context.GetService<AppSettings>();
        }

        [HttpGet]
        [Route("GetVersion")]
        public async Task<ActionResult<Version>> GetVersion()
        {
            await Task.Delay(0);
            var Version = typeof(OperateController).Assembly.GetName().Version;
            return Ok(Version);
        }

        [HttpGet]
        [Route("GetChargesWithoutCost")]
        public async Task<ActionResult<ChargingProcess>> GetChargesWithoutCost()
        {
            List<ChargingProcess> ChargesWithoutCost = await Tools.ListChargingProcessWithoutCost();
            return Ok(ChargesWithoutCost);
        }

        [HttpGet]
        [Route("GetIncompleteDrives")]
        public async Task<ActionResult<ChargingProcess>> GetIncompleteDrives()
        {
            List<Drive> IncompleteDrives = await Tools.IncompleteDrives();
            return Ok(IncompleteDrives);
        }

        [HttpGet]
        [Route("GetShortTimeBetweenDrives")]
        public async Task<ActionResult<ShortTimeDrive>> GetShortTimeBetweenDrives(int Minutes = 3)
        {
            var IncompleteDrives = await Task.Run(() => Tools.ShortTimeBetweenDrives(Minutes));
            return Ok(IncompleteDrives);
        }

        [HttpGet]
        [Route("ImportExcelFromUFD")]
        public async Task<ActionResult<int>> ImportExcelFromUFD()
        {
            int nRecords = await Tools.ImportExcelFromUFD();
            return Ok(nRecords);
        }

        [HttpPost]
        [Route("ProcessChargesAtHomeWithoutCost")]
        public async Task<ActionResult<ChargingProcess>> ProcessChargesAtHomeWithoutCost()
        {
            try
            {
                var ChargingProcessWithoutCostAtHome = await Task.Run(() => Tools.ProcessChargesAtHomeWithoutCost());
                return Ok(ChargingProcessWithoutCostAtHome);
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += string.Format(" - {0}", ex.InnerException.Message);
                return BadRequest(msg);
            }

        }

        [HttpPost]
        [Route("ProcessAndFixAddresses")]
        public async Task<ActionResult<int>> ProcessAndFixAddresses()
        {
            try
            {
                var nRecords = await Task.Run(() => Tools.ProcessAndFixAddresses());
                return Ok(nRecords);
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += string.Format(" - {0}", ex.InnerException.Message);
                return BadRequest(msg);
            }

        }

        [HttpPost]
        [Route("ProcessShortDrives")]
        public async Task<ActionResult<int>> ProcessShortDrives(int Minutes = 3)
        {
            try
            {
                var recProccesed = await Task.Run(() => Tools.ProcessShortDrives(Minutes));
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
        [Route("JoinCharges")]
        public async Task<ActionResult<ChargingProcess>> JoinCharges(int SourceID, int TargetID)
        {

            try
            {
                if (TargetID <= SourceID) throw new Exception("TargetID must be greater than SourceID");
                var Records = await Task.Run(() => Tools.MixCharges(SourceID, TargetID));
                return Ok(Records);
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += string.Format(" - {0}", ex.InnerException.Message);
                return BadRequest(msg);
            }
        }

        [HttpPost]
        [Route("JoinDrives")]
        public async Task<ActionResult<Drive>> JoinDrives(int SourceID, int TargetID)
        {
            try
            {
                if (TargetID <= SourceID) throw new Exception("TargetID must be greater than SourceID");
                var Records = await Task.Run(() => Tools.JoinDrives(SourceID, TargetID));
                return Ok(Records);
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += string.Format(" - {0}", ex.InnerException.Message);
                return BadRequest(msg);
            }
        }

        [HttpPost]
        [Route("GpxToWaze")]
        public async Task<ActionResult<string>> GpxToWaze(string XMLString)
        {
            try
            {
                var returnText = new StringBuilder();
                int counter = 0;
                var wptData = await Task.Run(() => Tools.XmltoWpt(XMLString));
                foreach (var wpt in wptData)
                {
                    //string[] row = { wpt.Name, wpt.Desc, wpt.Lat, wpt.Lon };
                    //tabDelimitedText.AppendLine(string.Join("\t", row));
                    //https://ul.waze.com/ul?ll=40.35164600,-3.69245800&navigate=yes
                    ++counter;
                    returnText.AppendLine(string.Format("{0}: {1}", counter.ToString("00"), wpt.Desc));
                    returnText.AppendLine(string.Format("https://ul.waze.com/ul?ll={0},{1}&navigate=yes", wpt.Lat, wpt.Lon));
                    returnText.AppendLine(string.Empty);
                }
                return Ok(returnText.ToString());
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += string.Format(" - {0}", ex.InnerException.Message);
                return BadRequest(msg);
            }
        }

        [HttpPost]
        [Route("GpxToGoogleMaps")]
        public async Task<ActionResult<string>> GpxToGoogleMaps(string XMLString)
        {
            try
            {
                var returnText = new StringBuilder();
                var wptData = await Task.Run(() => Tools.XmltoWpt(XMLString));

                // Read the last item
                var destinationPoint = wptData[wptData.Count - 1];

                returnText.Append(string.Format("https://www.google.com/maps/dir/?api=1&destination={0},{1}", destinationPoint.Lat, destinationPoint.Lon));

                for (int i = 0; i < wptData.Count - 1; i++)
                {
                    if (i == 0) // Only for the first waypoint
                    {
                        returnText.Append("&waypoints=");
                    }
                    var wayPoint = wptData[i];
                    // https://www.google.com/maps/dir/?api=1&destination=40.3456279,-3.6787393
                    // &waypoints=40.3679779,-3.5865541|40.3603668,-3.13592|40.5533149,-2.6710437|40.7578582,-2.8697202
                    // &travelmode=driving&dir_action=navigate
                    returnText.Append(string.Format("{0},{1}", wayPoint.Lat, wayPoint.Lon));
                    if (i < wptData.Count - 2) // All Except the last waypoint
                    {
                        returnText.Append("|");
                    }
                }
                returnText.Append("&travelmode=driving&dir_action=navigate");
                return Ok(returnText.ToString());
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += string.Format(" - {0}", ex.InnerException.Message);
                return BadRequest(msg);
            }
        }

        [HttpPost]
        [Route("UploadFile")]
        public async Task<ActionResult<string>> UploadFile(IFormFile file)
        {
            try
            {
                if (file != null && file.Length > 0)
                {
                    // Process the uploaded file (e.g., save it to disk, read its content, etc.)
                    // Example: Save the file to a specific location
                    var filePath = Path.Combine(appSettings.ImportPath, file.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }
                    // Other processing logic as needed
                    return Ok("File uploaded successfully!");
                }
                return BadRequest("No file was uploaded.");
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += string.Format(" - {0}", ex.InnerException.Message);
                return BadRequest(msg);
            }
        }

        [HttpPost]
        [Route("DownloadFile")]
        [ProducesResponseType(typeof(FileContentResult), 200)]
        public async Task<ActionResult> DownloadFile(string file)
        {
            try
            {
                // Read the file content (e.g., from disk, database, etc.)
                var fileBytes = await System.IO.File.ReadAllBytesAsync(Path.Combine(appSettings.ImportPath, file));

                // Set the appropriate content type and file name
                var contentType = "application/octet-stream"; // Adjust based on your file type
                var fileResult = new FileContentResult(fileBytes, contentType)
                {
                    FileDownloadName = file
                };

                return fileResult;
            }
            catch (Exception ex)
            {
                string msg = ex.Message;
                if (ex.InnerException != null) msg += string.Format(" - {0}", ex.InnerException.Message);
                return BadRequest(msg);
            }
        }
    }
}
