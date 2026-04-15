using Microsoft.Extensions.Configuration;
using System;

namespace FPTU.Capstone.AMKCollective.Application.Helpers
{
    public static class DateTimeHelper
    {
        private static TimeZoneInfo _localTimeZone;

        public static void Initialize(IConfiguration configuration)
        {
            string winTz = configuration["TimeZoneSettings:Windows"];
            string ianaTz = configuration["TimeZoneSettings:Iana"];
            double offsetHours = configuration.GetValue<double>("TimeZoneSettings:FallbackOffsetHours", 7);

            _localTimeZone = GetLocalTimeZone(winTz, ianaTz, offsetHours);
        }

        private static TimeZoneInfo GetLocalTimeZone(string winTz, string ianaTz, double offsetHours)
        {
            try
            {
                // Windows
                return TimeZoneInfo.FindSystemTimeZoneById(winTz);
            }
            catch (TimeZoneNotFoundException)
            {
                try
                {
                    // Linux / macOS / Android
                    return TimeZoneInfo.FindSystemTimeZoneById(ianaTz);
                }
                catch (TimeZoneNotFoundException)
                {
                    // Fallback to custom offset if both fail
                    return TimeZoneInfo.CreateCustomTimeZone("LocalTime", TimeSpan.FromHours(offsetHours), $"(GMT+{offsetHours}) Local Time", "Local Time");
                }
            }
        }

        private static TimeZoneInfo LocalTimeZone => _localTimeZone ?? GetLocalTimeZone("SE Asia Standard Time", "Asia/Ho_Chi_Minh", 7);

        public static DateTime GetLocalTimeNow()
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, LocalTimeZone);
        }

        public static DateTime ConvertToLocalTime(this DateTime utcDateTime)
        {
            if (utcDateTime.Kind == DateTimeKind.Local)
            {
                throw new ArgumentException("Expected UTC DateTime, but got Local DateTime.", nameof(utcDateTime));
            }
            
            return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, LocalTimeZone);
        }

        public static DateTime ConvertToUtcTime(this DateTime localDateTime)
        {
            return TimeZoneInfo.ConvertTimeToUtc(localDateTime, LocalTimeZone);
        }
    }
}

