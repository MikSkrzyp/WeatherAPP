using System.ComponentModel.DataAnnotations;

namespace WeatherApplication.Models
{
    public class WeatherForecast
    {

        [Required(ErrorMessage = "City is required")]
        [StringLength(20, ErrorMessage = "City should be at most 20 characters")]
        [RegularExpression("^[a-zA-Z ]*$", ErrorMessage = "City should not contain numbers and special characters (except space)")]
        public string City { get; set; }
        public string Temperature { get; set; }
        public string Description { get; set; }
    }

}
