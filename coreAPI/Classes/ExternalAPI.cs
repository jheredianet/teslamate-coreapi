using coreAPI.Models;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

namespace coreAPI.Classes
{
    public class ExternalAPI
    {
        public static async Task<OutChargeProgramming?> CallCalculateChargePlanByKwh(InChargeProgramming cp)
        {
            return await GetResponseFromApi("calculate_charge_plan_bykwh", cp);
        }

        public static async Task<OutChargeProgramming?> CallCalculateChargePlan(InChargeProgramming cp)
        {
            return await GetResponseFromApi("calculate_charge_plan", cp);
        }

        public static async Task<OutChargeProgramming?> CallCalculateOnCheapeastHours()
        {
            return await GetResponseFromApi("calculate_by_pvpc");
        }

        public static async Task<OutChargeProgramming?> GetChargePlan()
        {
            return await GetResponseFromApi("get_charge_plan");
        }

        public static async Task<OutChargeProgramming?> ApplyChargePlan()
        {
            return await GetResponseFromApi("apply_charge_plan");
        }

        private static async Task<OutChargeProgramming?> GetResponseFromApi(string command, InChargeProgramming? cp = null)
        {
            var programmingData = new OutChargeProgramming();
            try
            {
                string apiUrl = "https://nodered.infoinnova.net/"; // Replace with your API URL
                string username = "jchm";
                string password = "mzMvmu3kC9H8wgwR";

                // Create an HttpClient instance
                using var client = new HttpClient();
                client.BaseAddress = new Uri(apiUrl);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Set basic authentication header
                var plainTextBytes = Encoding.UTF8.GetBytes($"{username}:{password}");
                string base64Credentials = Convert.ToBase64String(plainTextBytes);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", base64Credentials);

                client.DefaultRequestHeaders.Add("Command", command);

                var jsonPayload = string.Empty;
                if (cp != null)
                {
                    // Create your JSON payload (replace with your actual data)
                    jsonPayload = JsonConvert.SerializeObject(cp);
                }


                // Send a POST request with the JSON payload
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("customapi", content);

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = await response.Content.ReadAsStringAsync();
                    programmingData = JsonConvert.DeserializeObject<OutChargeProgramming>(responseBody);
                }
                else
                {
                    Console.WriteLine($"Error: {response.StatusCode} - {response.ReasonPhrase}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
            return programmingData;
        }
    }
}
