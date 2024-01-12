using System.ComponentModel.DataAnnotations;

namespace WeatherApplication.Models
{
 
    public class WeatherForecast
    {
       
        public string City { get; set; }
        public string Temperature { get; set; }
        public string Description { get; set; }
    }

}
