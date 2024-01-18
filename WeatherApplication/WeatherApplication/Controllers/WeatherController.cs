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
    // This controller requires the "user" role for authorization
    [Authorize(Roles = "user")]
    public class WeatherController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;
        private readonly WeatherDbContext _dbContext;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly IMemoryCache _cache;


        // Constructor for WeatherController
        public WeatherController(IMemoryCache cache, IHttpClientFactory clientFactory, WeatherDbContext dbContext,UserManager<IdentityUser> userManager)
        {
            // Initialize dependencies through dependency injection
            _clientFactory = clientFactory;
            _dbContext = dbContext;
            _userManager = userManager;
            _cache = cache;

        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // Get the current user using UserManager
            var currentUser = await _userManager.GetUserAsync(User);

            // Retrieve serialized weather data from TempData
            string jsonData = TempData["WeatherData"] as string;

            if (jsonData != null)
            {
                // Deserialize the JSON data into a Tuple
                Tuple<List<WeatherData>, WeatherForecast> data = JsonConvert.DeserializeObject<Tuple<List<WeatherData>, WeatherForecast>>(jsonData);

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

                    // Set the retrieved data in the cache with a sliding expiration of 5 seconds
                    var cacheEntryOptions = new MemoryCacheEntryOptions()
                        .SetSlidingExpiration(TimeSpan.FromSeconds(5));
                    _cache.Set(cacheKey, weatherData, cacheEntryOptions);
                }

                // Stop timer after loading data
                var endTime = DateTime.UtcNow;

                // Calculate elapsed time
                var elapsedTime = endTime - startTime;

                // Log elapsed time 
                Console.WriteLine($"Loading data from cache took {elapsedTime.TotalMilliseconds} milliseconds");

                // Return the retrieved or newly created Tuple to the View
                return View(new Tuple<List<WeatherData>, WeatherForecast>(weatherData, new WeatherForecast()));
            }
        }
        [HttpPost]
        public async Task<IActionResult> Index(string city)
        {
            // Load environment variables using DotNetEnv library
            DotNetEnv.Env.Load();

            // Get the current user
            var user = await _userManager.GetUserAsync(User);

            // Retrieve API key from environment variables
            var apiKey = Environment.GetEnvironmentVariable("API_KEY");

            // Create an HTTP request to OpenWeatherMap API
            var request = new HttpRequestMessage(HttpMethod.Get,
                $"http://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric");

            // Create an HTTP client using the injected factory
            var client = _clientFactory.CreateClient();

            // Send the request to OpenWeatherMap API
            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                // Read the response stream as a string
                var responseStream = await response.Content.ReadAsStringAsync();

                // Deserialize the JSON response from OpenWeatherMap API
                dynamic data = JsonConvert.DeserializeObject(responseStream);

                // Extract weather information from the API response
                WeatherForecast weather = new WeatherForecast
                {
                    City = data.name,
                    Temperature = data.main.temp,
                    Description = data.weather[0].description
                };

                // Create a new WeatherData object to store in the database
                var weatherData = new WeatherData
                {
                    City = weather.City,
                    Temperature = weather.Temperature.ToString(),
                    Description = weather.Description,
                    email = user.Email
                };

                // Add the WeatherData to the database
                _dbContext.WeatherData.Add(weatherData);
                await _dbContext.SaveChangesAsync();

                // Retrieve all weather data for the current user from the database
                var allWeatherData = _dbContext.WeatherData
                    .Where(w => w.email == user.Email)
                    .OrderByDescending(w => w.Id).ToList();

                // Create an AdminLogs entry for logging the action
                var adminLogs = new AdminLogs
                {
                    Email = user.Email,
                    City = weather.City,
                    Temperature = weather.Temperature.ToString(),
                    Time = DateTime.Now,
                    Action = "Add"
                };

                // Add the AdminLogs entry to the database
                _dbContext.AdminLogs.Add(adminLogs);
                await _dbContext.SaveChangesAsync();

                // Create a cache key for the user's weather data
                var cacheKey = $"weatherData_{user.Email}";

                // Remove cached weather data for the user
                _cache.Remove(cacheKey);

                // Redirect to the "Index" action with updated data stored in TempData
                TempData["WeatherData"] = JsonConvert.SerializeObject(new Tuple<List<WeatherData>, WeatherForecast>(allWeatherData, weather));
                return RedirectToAction(nameof(Index));
            }
            else
            {
                // Set an error message in TempData for display on the Index view
                TempData["ErrorMessage"] = "Failed to retrieve weather data. Please try again.";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            // Get the current user
            var user = await _userManager.GetUserAsync(User);

            // Find the WeatherData entry with the specified id in the database
            var weatherData = await _dbContext.WeatherData.FindAsync(id);

            // Log the deletion action in the AdminLogs database table
            var adminLogs = new AdminLogs
            {
                Email = user.Email,
                City = weatherData.City,
                Temperature = weatherData.Temperature.ToString(),
                Time = System.DateTime.Now,
                Action = "Delete"
            };

            // Add the AdminLogs entry to the database
            _dbContext.AdminLogs.Add(adminLogs);
            await _dbContext.SaveChangesAsync();

            // Check if the WeatherData entry with the specified id exists
            if (weatherData == null)
            {
                // If not found, return a 404 Not Found response
                return NotFound();
            }

            // Remove the WeatherData entry from the database
            _dbContext.WeatherData.Remove(weatherData);
            await _dbContext.SaveChangesAsync();

            // Create a cache key for the user's weather data
            var cacheKey = $"weatherData_{user.Email}";

            // Remove cached weather data for the user
            _cache.Remove(cacheKey);

            // Redirect to the "Index" action after successful deletion
            return RedirectToAction(nameof(Index));
        }


        // Ensure that only users with the "admin" role can access these actions
        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            // Find the user by their ID
            var user = await _userManager.FindByIdAsync(userId);

            // Check if the user with the specified ID exists
            if (user == null)
            {
                // If not found, return a 404 Not Found response
                return NotFound();
            }

            // Find and delete associated logs for the user
            var userLogs = _dbContext.AdminLogs.Where(log => log.Email == user.Email);
            _dbContext.AdminLogs.RemoveRange(userLogs);

            // Find and delete associated weather data for the user
            var userWeatherData = _dbContext.WeatherData.Where(i => i.email == user.Email);
            _dbContext.WeatherData.RemoveRange(userWeatherData);

            // Attempt to delete the user
            var result = await _userManager.DeleteAsync(user);

            if (!result.Succeeded)
            {
                // Handle error (could not delete user)
                // You might want to log or return an error message
                return BadRequest("Unable to delete user.");
            }

            // Save changes to persist the deletion of both user and associated logs
            await _dbContext.SaveChangesAsync();

            // User deletion successful, redirect to AdminUsers action
            return RedirectToAction(nameof(AdminUsers));
        }

        // Ensure that only users with the "admin" role can access this action
        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> DeleteLog(string id)
        {
            // Check if the provided ID can be parsed to an integer
            if (!int.TryParse(id, out int logId))
            {
                // Handle invalid ID (not a valid integer)
                return BadRequest("Invalid ID format");
            }

            // Find the log by its ID in the database
            var log = await _dbContext.AdminLogs.FindAsync(logId);

            // Check if the log with the specified ID exists
            if (log == null)
            {
                // If not found, return a 404 Not Found response
                return NotFound();
            }

            // Remove the log from the database
            _dbContext.AdminLogs.Remove(log);

            // Save changes to persist the deletion
            await _dbContext.SaveChangesAsync();

            // Redirect to the action that displays AdminLogs
            return RedirectToAction(nameof(AdminLogs));
        }

        // Ensure that only users with the "admin" role can access this action
        [Authorize(Roles = "admin")]
        [HttpGet]
        public IActionResult AdminLogs()
        {
            // Retrieve all logs data from the database and order it by ID in descending order
            var allLogsData = _dbContext.AdminLogs.OrderByDescending(w => w.Id).ToList();

            // Return the AdminLogs view with the retrieved data
            return View(allLogsData);
        }

        // Ensure that only users with the "admin" role can access this action
        [Authorize(Roles = "admin")]
        [HttpGet]
        public IActionResult AdminUsers()
        {
            // Retrieve all users data from the UserManager
            var allUsersData = _userManager.Users.ToList();

            // Exclude the user with email 'admin@admin.com' from the list
            var filteredUsers = allUsersData.Where(user => user.Email != "admin@admin.com").ToList();

            // Return the AdminUsers view with the filtered users data
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
