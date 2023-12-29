using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WeatherApplication.Models;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace WeatherApplication.Controllers
{
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
            var weatherData = _dbContext.WeatherData.ToList();
            return View(new Tuple<List<WeatherData>, WeatherForecast>(weatherData, new WeatherForecast()));
        }

        [HttpPost]
        public async Task<IActionResult> Index(string city)
        {
            var apiKey = "Insert Here Api Key";
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
                var allWeatherData = _dbContext.WeatherData.ToList();

                return View(new Tuple<List<WeatherData>, WeatherForecast>(allWeatherData, weather));
            }
            else
            {
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
