using System.Reflection;

namespace SSH_Helper
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Initialize Rebex license key - try embedded metadata first (from build),
            // then local rebex.key file (for dev), then env var
            var rebexKey = Assembly.GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "RebexLicenseKey")?.Value;

            // Check for local rebex.key file (useful for development)
            if (string.IsNullOrEmpty(rebexKey))
            {
                var keyFilePath = Path.Combine(AppContext.BaseDirectory, "rebex.key");
                if (File.Exists(keyFilePath))
                {
                    rebexKey = File.ReadAllText(keyFilePath).Trim();
                }
            }

            // Fall back to environment variable
            if (string.IsNullOrEmpty(rebexKey))
            {
                rebexKey = Environment.GetEnvironmentVariable("REBEX_LICENSE_KEY");
            }

            if (!string.IsNullOrEmpty(rebexKey))
            {
                Rebex.Licensing.Key = rebexKey;
            }

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}