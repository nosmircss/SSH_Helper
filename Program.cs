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
            // Initialize Rebex license key from environment variable
            var rebexKey = Environment.GetEnvironmentVariable("REBEX_LICENSE_KEY");
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