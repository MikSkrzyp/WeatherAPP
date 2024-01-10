
namespace WeatherApplication.Models
{
    public class AdminLogs
    {
        public int Id { get; set; }
        public string Email { get; set; }
        public string City { get; set; }
        public string Temperature { get; set; }
        public DateTime Time { get; set; }
        public string Action { get; set; }
    }

}
