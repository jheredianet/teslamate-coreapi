namespace coreAPI.Models
{
    public class AppSettings
    {
        public string ImportPath { get; set; } = string.Empty;
        public string CurrentPath { get; set; } = Directory.GetCurrentDirectory();


        public AppSettings()
        {
        }


        public AppSettings(AppSettings settings)
        {
            foreach (var property in settings.GetType().GetProperties())
            {
                var value = property.GetValue(settings, null);
                GetType().GetProperty(property.Name)?.SetValue(this, value);
            }
        }
    }
}