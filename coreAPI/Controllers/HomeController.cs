using coreAPI.Classes;
using coreAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace coreAPI.Controllers
{
    public class HomeController : Controller
    {
        // GET: Home/Index
        public ActionResult Index()
        {
            // Call External API
            var DataPlan = ExternalAPI.GetChargePlan().Result;
            var model = new VehicleChargingViewModel();
            // Ensure DataPlan is not null before passing it to the method
            if (DataPlan != null)
            {
                model = AssignValuesFromOutChargeProgrammingToViewModel(model, DataPlan);
            }
            else
            {
                // Handle the case where DataPlan is null (e.g., log an error, return an error view, etc.)
                ModelState.AddModelError(string.Empty, "Failed to retrieve charging plan.");
            }
            return View(model);
        }

        [HttpPost]
        public ActionResult UpdateProgramming(VehicleChargingViewModel model)
        {
            InChargeProgramming cp;
            OutChargeProgramming? DataPlan = null;

            if (model.Mode == "Current")
            {
                cp = new InChargeProgramming
                {
                    Current = model.Current,
                    TargetSOC = model.SOCDesired,
                    DepartureTime = model.DepartureTime
                };
                // Call External API
                DataPlan = ExternalAPI.CallCalculateChargePlan(cp).Result;
            }
            else // kWh
            {
                cp = new InChargeProgramming
                {
                    Current = model.Current, // This value is for  kWh
                    //TargetSOC = model.SOCDesired,
                    DepartureTime = model.DepartureTime
                };
                // Call External API
                DataPlan = ExternalAPI.CallCalculateChargePlanByKwh(cp).Result;
            }


            // Ensure DataPlan is not null before passing it to the method
            if (DataPlan != null)
            {
                model = AssignValuesFromOutChargeProgrammingToViewModel(model, DataPlan);
            }
            else
            {
                // Handle the case where DataPlan is null (e.g., log an error, return an error view, etc.)
                ModelState.AddModelError(string.Empty, "Failed to retrieve charging plan.");
            }

            return View("Index", model);
        }

        [HttpPost]
        public ActionResult ChargeOnCheapeastHours(VehicleChargingViewModel model)
        {
            // Call External API
            var DataPlan = ExternalAPI.CallCalculateOnCheapeastHours().Result;

            // Ensure DataPlan is not null before passing it to the method
            if (DataPlan != null)
            {
                model = AssignValuesFromOutChargeProgrammingToViewModel(model, DataPlan);
                TempData["SuccessMessage"] = "Charging plan calculated successfully!"; // Set success message
            }
            else
            {
                // Handle the case where DataPlan is null (e.g., log an error, return an error view, etc.)
                ModelState.AddModelError(string.Empty, "Failed to retrieve charging plan.");
                TempData["ErrorMessage"] = "Failed to retrieve charging plan."; // Set error message
            }

            return View("Index", model);
        }

        //[HttpPost]
        //public ActionResult UpdateCheaperHours(VehicleChargingViewModel model)
        //{
        //    return RedirectToAction("Index");
        //}

        [HttpPost]
        public ActionResult ApplyActualSchedule(VehicleChargingViewModel model)
        {
            // Call External API
            var DataPlan = ExternalAPI.ApplyChargePlan().Result;

            // Ensure DataPlan is not null before passing it to the method
            if (DataPlan != null)
            {
                model = AssignValuesFromOutChargeProgrammingToViewModel(model, DataPlan);
            }
            else
            {
                // Handle the case where DataPlan is null (e.g., log an error, return an error view, etc.)
                ModelState.AddModelError(string.Empty, "Failed to retrieve charging plan.");
            }
            return View("Index", model);
        }

        private VehicleChargingViewModel AssignValuesFromOutChargeProgrammingToViewModel(VehicleChargingViewModel model, OutChargeProgramming DataPlan)
        {
            model.Current = DataPlan.Amp;
            model.ChargingPlanStart = DataPlan.Start;
            model.ChargingPlanEnd = DataPlan.End;
            model.ChargingPlanTime = DataPlan.Time;
            model.FromPercentage = Convert.ToInt32(DataPlan.From);
            model.ToPercentage = Convert.ToInt32(DataPlan.To);
            model.ToAddPercentage = Convert.ToInt32(DataPlan.ToAdd);
            model.Add = DataPlan.Added;
            model.Used = DataPlan.Used;
            model.Output = DataPlan.Output;
            model.Intake = DataPlan.Intake;
            model.Charge = DataPlan.Charge;
            model.RangeAdded = DataPlan.RangeAdded;
            model.BatteryCapacity = DataPlan.BatteryCapacity;
            model.SOCDesired = model.ToPercentage;
            model.DepartureTime = DataPlan.End.ToLocalTime().ToString("HH:mm"); // Format End date as "HH:mm"

            // Ensure Schedule is not null before accessing its properties
            if (DataPlan.Schedule != null)
            {
                model.ActualStart = DataPlan.Schedule.StartTime;
                model.ActualEnd = DataPlan.Schedule.DepartureTime;
                model.ActualTime = DataPlan.Schedule.DurationTime;
            }
            else
            {
                // Handle the case where Schedule is null (e.g., set default values or log an error)
                model.ActualStart = DateTime.MinValue;
                model.ActualEnd = DateTime.MinValue;
                model.ActualTime = string.Empty;
            }

            return model;
        }
    }
}