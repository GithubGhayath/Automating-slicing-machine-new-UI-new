using System.Configuration;

namespace MR200.UI.Helpers
{
    public static class clsHelper
    {
        public static string ReadFromConfiguration(string Key)
        {
            return ConfigurationManager.AppSettings[Key]?.ToString() ?? string.Empty;
        }

        public static double DegreesToRadians(double degrees) => degrees * Math.PI / 180.0;
        public static double RadiansToDegrees(double radians) => radians * 180.0 / Math.PI;
        public static double MillimeterToMeter(double millimeters) => millimeters / 1000.0;
        public static double MeterPerSecToMeterPerMin(double mPerSec) => mPerSec * 60.0;
        public static double MeterPerMinToMeterPerSec(double mPerMin) => mPerMin / 60.0;
    }
}
