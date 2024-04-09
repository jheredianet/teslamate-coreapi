using coreAPI.Classes;
using coreAPI.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace coreAPI.Controllers
{
    public class HomeController : Controller
    {
        //private readonly ILogger<HomeController> _logger;

        //public HomeController(ILogger<HomeController> logger)
        //{
        //    _logger = logger;
        //}

        public IActionResult Index()
        {
            return View();
        }
        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        [HttpPost]
        //public IActionResult CalculateChargeProgramaming(ChargeProgramming cp)
        public IActionResult Index(InChargeProgramming cp)
        {
            // Call External API
            var DataPlan = ExternalAPI.CallCalculateChargePlan(cp).Result;

            if (DataPlan == null)
            {
                return View();
            }
            else
            {
                DataPlan.Current = cp.Current;
                DataPlan.TargetSOC = cp.TargetSOC;
                DataPlan.DepartureTime = cp.DepartureTime;
                return View(DataPlan);
            }
        }
    }
}
