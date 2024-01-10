using Microsoft.EntityFrameworkCore;

namespace WeatherApplication.Models
{
    public class WeatherDbContext : DbContext
    {
        public WeatherDbContext(DbContextOptions<WeatherDbContext> options) : base(options)
        {
        }

        public DbSet<WeatherData> WeatherData { get; set; }
        public DbSet<AdminLogs> AdminLogs { get; set; }
    }

}
