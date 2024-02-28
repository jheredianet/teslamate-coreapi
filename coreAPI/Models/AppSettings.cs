namespace coreAPI.Models
{
    public class AppSettings
    {
        public string ImportPath { get; set; } = string.Empty;
        public string CurrentPath { get; set; } = Directory.GetCurrentDirectory();
        public string sshServer { get; set; } = string.Empty;
        public int sshPort { get; set; } = 22;
        public string sshUserName { get; set; } = string.Empty;
        public string sshKeyFile { get; set; } = string.Empty;

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