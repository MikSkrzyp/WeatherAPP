using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using WeatherApplication.Models;

namespace WeatherApplication.Controllers
{
    public class WeatherController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        public WeatherController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View(new WeatherForecast());
        }

        [HttpPost]
        public async Task<IActionResult> Index(string city)
        {
            var apiKey = "InsertHereYourApiKey";
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"http://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric");
            var client = _clientFactory.CreateClient();
            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseStream = await response.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(responseStream);
                WeatherForecast weather = new WeatherForecast();
                weather.City = data.name;
                weather.Temperature = data.main.temp; 
                weather.Description = data.weather[0].description;
                return View(weather);
            }
            else
            {
                return View(new WeatherForecast());
            }
        }

    }



}
