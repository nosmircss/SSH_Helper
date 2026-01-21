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
            // Initialize Rebex license key
            // Priority: 1) Environment variable (CI/CD), 2) Local file (dev)
            var rebexKey = Environment.GetEnvironmentVariable("REBEX_LICENSE_KEY");
            if (string.IsNullOrEmpty(rebexKey))
            {
                // Load from gitignored file for local development
                var keyFile = Path.Combine(AppContext.BaseDirectory, "rebex.key");
                if (File.Exists(keyFile))
                {
                    rebexKey = File.ReadAllText(keyFile).Trim();
                }
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