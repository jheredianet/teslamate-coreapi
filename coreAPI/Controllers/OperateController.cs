using coreAPI.Classes;
using coreAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace coreAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OperateController : ControllerBase
    {
        private readonly teslamateContext _context;
        private readonly ToolsData Tools;

        public OperateController(teslamateContext context)
        {
            _context = context;
            Tools = new ToolsData(_context);
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
        [Route("GetCharges")]
        public async Task<ActionResult<ChargingProcess>> GetCharges()
        {
            List<ChargingProcess> lastFiveCharges = await Tools.ListLastCharges();
            return Ok(lastFiveCharges);
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

    }
}
