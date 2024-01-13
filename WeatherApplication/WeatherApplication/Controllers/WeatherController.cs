using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using WeatherApplication.Models;
using System.Net.Http;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;

using System;



namespace WeatherApplication.Controllers
{
    [Authorize(Roles = "user")]
    public class WeatherController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly WeatherDbContext _dbContext;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IMemoryCache _cache;


        public WeatherController(IMemoryCache cache, IHttpClientFactory clientFactory, WeatherDbContext dbContext,UserManager<IdentityUser> userManager)
        {
            _clientFactory = clientFactory;
            _dbContext = dbContext;
            _userManager = userManager;
            _cache = cache;

        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            string jsonData = TempData["WeatherData"] as string;

            if (jsonData != null)
            {
                Tuple<List<WeatherData>, WeatherForecast> data = JsonConvert.DeserializeObject<Tuple<List<WeatherData>, WeatherForecast>>(jsonData);
                // Your existing code
                return View(data);
            }
            else
            {
                // Use the user's email as part of the cache key
                var cacheKey = $"weatherData_{currentUser.Email}";

                // Start timer before loading data
                var startTime = DateTime.UtcNow;

                if (!_cache.TryGetValue(cacheKey, out List<WeatherData> weatherData))
                {
                    // Retrieve weather data from the database
                    weatherData = _dbContext.WeatherData
                        .Where(w => w.email == currentUser.Email)
                        .OrderByDescending(w => w.Id).ToList();

                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromSeconds(5));
                    _cache.Set(cacheKey, weatherData, cacheEntryOptions);
                }

                // Stop timer after loading data
                var endTime = DateTime.UtcNow;

                // Calculate elapsed time
                var elapsedTime = endTime - startTime;

                // Log elapsed time (you can replace Console.WriteLine with your preferred logging mechanism)
                Console.WriteLine($"Loading data from cache took {elapsedTime.TotalMilliseconds} milliseconds");

                return View(new Tuple<List<WeatherData>, WeatherForecast>(weatherData, new WeatherForecast()));
            }
        }

        [HttpPost]
        public async Task<IActionResult> Index(string city)
        {
            DotNetEnv.Env.Load();
            var user = await _userManager.GetUserAsync(User);

            var apiKey = "9b61e791ac55978d74b4c0372ad11745";

            var request = new HttpRequestMessage(HttpMethod.Get,
                $"http://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric");

            var client = _clientFactory.CreateClient();
            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var responseStream = await response.Content.ReadAsStringAsync();
                dynamic data = JsonConvert.DeserializeObject(responseStream);

                WeatherForecast weather = new WeatherForecast
                {
                    City = data.name,
                    Temperature = data.main.temp,
                    Description = data.weather[0].description
                };

                var weatherData = new WeatherData
                {
                    City = weather.City,
                    Temperature = weather.Temperature.ToString(),
                    Description = weather.Description,
                    email = user.Email
                };

                _dbContext.WeatherData.Add(weatherData);
                await _dbContext.SaveChangesAsync();

                var allWeatherData = _dbContext.WeatherData
                    .Where(w => w.email == user.Email)
                    .OrderByDescending(w => w.Id).ToList();

                var adminLogs = new AdminLogs
                {
                    Email = user.Email,
                    City = weather.City,
                    Temperature = weather.Temperature.ToString(),
                    Time = DateTime.Now,
                    Action = "Add"
                };

                _dbContext.AdminLogs.Add(adminLogs);
                await _dbContext.SaveChangesAsync();

                var cacheKey = $"weatherData_{user.Email}";

                // Remove cached weather data for the user
                _cache.Remove(cacheKey);

                // Redirect to the "Index" action with updated data
                TempData["WeatherData"] = JsonConvert.SerializeObject(new Tuple<List<WeatherData>, WeatherForecast>(allWeatherData, weather));
                return RedirectToAction(nameof(Index));
            }
            else
            {
                TempData["ErrorMessage"] = "Failed to retrieve weather data. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }


        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            //get current user
            var user = await _userManager.GetUserAsync(User);
            var weatherData = await _dbContext.WeatherData.FindAsync(id);


            // Logowanie danych do bazy danych
            var adminLogs = new AdminLogs
            {
                Email = user.Email,
                City = weatherData.City,
                Temperature = weatherData.Temperature.ToString(),
                Time = System.DateTime.Now,
                Action = "Delete"
            };

            _dbContext.AdminLogs.Add(adminLogs);
            await _dbContext.SaveChangesAsync();




            if (weatherData == null)
            {
                return NotFound();
            }

            _dbContext.WeatherData.Remove(weatherData);
            await _dbContext.SaveChangesAsync();





            var cacheKey = $"weatherData_{user.Email}";

            // Remove cached weather data for the user
            _cache.Remove(cacheKey);

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
            {
                return NotFound();
            }

            // Find and delete associated logs for the user
            var userLogs = _dbContext.AdminLogs.Where(log => log.Email == user.Email);
            _dbContext.AdminLogs.RemoveRange(userLogs);
            var userWeatherData = _dbContext.WeatherData.Where(i => i.email == user.Email);
            _dbContext.WeatherData.RemoveRange(userWeatherData);

            // Delete the user
            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                // Handle error (could not delete user)
                // You might want to log or return an error message
                return BadRequest("Unable to delete user.");
            }

            // Save changes to persist the deletion of both user and associated logs
            await _dbContext.SaveChangesAsync();

            // User deletion successful
            return RedirectToAction(nameof(AdminUsers));
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteLog(string id)
        {

            if (!int.TryParse(id, out int logId))
            {
                // Handle invalid ID (not a valid integer)
                return BadRequest("Invalid ID format");
            }

            var log = await _dbContext.AdminLogs.FindAsync(logId);

            if (log == null)
            {
                return NotFound();
            }

            _dbContext.AdminLogs.Remove(log);
            await _dbContext.SaveChangesAsync(); // Save changes to persist the deletion

            // Redirect to the action that displays AdminLogs
            return RedirectToAction(nameof(AdminLogs));
        }



        [Authorize(Roles = "admin")]
        [HttpGet]
        public IActionResult AdminLogs()
        {
            // Pobierz wszystkie dane z bazy danych
            var allLogsData = _dbContext.AdminLogs.OrderByDescending(w => w.Id).ToList();
            return View(allLogsData);
        }
        [Authorize(Roles = "admin")]
        [HttpGet]
        public IActionResult AdminUsers()
        {
            // Pobierz wszystkie dane z bazy danych
            var allUsersData = _userManager.Users.ToList();

            // Exclude the user with email 'admin@admin.com'
            var filteredUsers = allUsersData.Where(user => user.Email != "admin@admin.com").ToList();

            return View(filteredUsers);
        }
        [HttpGet]
        public IActionResult CacheCheck()
        {
            var currentUser = _userManager.GetUserAsync(User).Result;
            var cacheKey = $"weatherData_{currentUser.Email}";

            if (_cache.TryGetValue(cacheKey, out List<WeatherData> cachedWeatherData))
            {
                ViewBag.Message = "Cache is working!";
                return View(new Tuple<List<WeatherData>, WeatherForecast>(cachedWeatherData, new WeatherForecast()));
            }
            else
            {
                ViewBag.Message = "Cache is empty or expired. Try fetching data first.";
                return View();
            }
        }


    }
}
