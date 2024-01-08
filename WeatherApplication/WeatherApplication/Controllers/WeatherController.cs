using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WeatherApplication.Models;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;

namespace WeatherApplication.Controllers
{
    [Authorize(Roles = "user")]
    public class WeatherController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly WeatherDbContext _dbContext;

        public WeatherController(IHttpClientFactory clientFactory, WeatherDbContext dbContext)
        {
            _clientFactory = clientFactory;
            _dbContext = dbContext;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Creating a list of names
            List<string> simpleNames = new List<string>
        {
            "Alice",
            "Bob",
            "Charlie",
            "David",
            "Emma",
            "Frank",
            "Grace",
            "Henry",
            "Isabel",
            "Jack"
        };
            var weatherData = _dbContext.WeatherData.OrderByDescending(w => w.Id).ToList();
            return View(new Tuple<List<WeatherData>, WeatherForecast,List<string>>(weatherData, new WeatherForecast(),simpleNames));
        }

        [HttpPost]
        public async Task<IActionResult> Index(string city)
        {
            var apiKey = "9b61e791ac55978d74b4c0372ad11745";
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

                // Zapisywanie dane do bazy danych
                var weatherData = new WeatherData
                {
                    City = weather.City,
                    Temperature = weather.Temperature.ToString(),
                    Description = weather.Description
                };
                _dbContext.WeatherData.Add(weatherData);
                await _dbContext.SaveChangesAsync();

                // Pobierz wszystkie dane z bazy danych
                var allWeatherData = _dbContext.WeatherData.OrderByDescending(w => w.Id).ToList();

                return View(new Tuple<List<WeatherData>, WeatherForecast>(allWeatherData, weather));
            }
            else
            {
                
                TempData["ErrorMessage"] = "Failed to retrieve weather data. Please try again.";
                return View(new Tuple<List<WeatherData>, WeatherForecast>(_dbContext.WeatherData.ToList(), new WeatherForecast()));
            }
        }
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var weatherData = await _dbContext.WeatherData.FindAsync(id);
            if (weatherData == null)
            {
                return NotFound();
            }

            _dbContext.WeatherData.Remove(weatherData);
            await _dbContext.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }


    }
}
