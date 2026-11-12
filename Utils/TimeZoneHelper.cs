using System;
using System.Text.RegularExpressions;

namespace LoggingClient.Utils
{
    /// <summary>
    /// Utilidades de zona horaria parametrizable para estandarizar el registro temporal
    /// en logs (por defecto hora de Colombia UTC-5, parametrizable a cualquier zona horaria).
    /// </summary>
    public static class TimeZoneHelper
    {
        private static TimeZoneInfo _currentTimeZone = ResolveTimeZone("America/Bogota");

        /// <summary>
        /// Obtiene la zona horaria actualmente configurada para los logs (por defecto Colombia, UTC-5).
        /// </summary>
        public static TimeZoneInfo CurrentTimeZone => _currentTimeZone;

        /// <summary>
        /// Identificador de la zona horaria actual.
        /// </summary>
        public static string CurrentTimeZoneId => _currentTimeZone.Id;

        /// <summary>
        /// Configura la zona horaria global para el cliente de logging.
        /// Acepta identificadores IANA (ej. "America/Bogota", "America/New_York", "Europe/Madrid")
        /// o nombres de Windows (ej. "SA Pacific Standard Time", "Eastern Standard Time").
        /// </summary>
        public static void SetTimeZone(string? timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                _currentTimeZone = ResolveTimeZone("America/Bogota");
                return;
            }

            _currentTimeZone = ResolveTimeZone(timeZoneId.Trim());
        }

        /// <summary>
        /// Configura directamente un TimeZoneInfo específico.
        /// </summary>
        public static void SetTimeZone(TimeZoneInfo timeZone)
        {
            _currentTimeZone = timeZone ?? throw new ArgumentNullException(nameof(timeZone));
        }

        /// <summary>
        /// Configura una zona horaria por desfase horario fijo (TimeSpan).
        /// </summary>
        public static void SetTimeZone(TimeSpan offset, string name = "CustomTimeZone")
        {
            _currentTimeZone = TimeZoneInfo.CreateCustomTimeZone(name, offset, name, name);
        }

        /// <summary>
        /// Resuelve de manera tolerante a fallos la zona horaria en cualquier sistema operativo.
        /// </summary>
        public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
        {
            // 1. Intento directo con el identificador provisto
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch { }

            // 2. Mapeos bidireccionales comunes IANA <-> Windows
            var mappedId = MapIanaOrWindowsId(timeZoneId);
            if (!string.IsNullOrEmpty(mappedId))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById(mappedId);
                }
                catch { }
            }

            // 3. Intento de parsear formato UTC offset (ej: "UTC-5", "-05:00", "+01:00")
            var match = Regex.Match(timeZoneId, @"^(?:UTC)?([+-])(\d{1,2})(?::?(\d{2}))?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var sign = match.Groups[1].Value == "-" ? -1 : 1;
                var hours = int.Parse(match.Groups[2].Value);
                var minutes = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
                var offset = new TimeSpan(sign * hours, sign * minutes, 0);
                return TimeZoneInfo.CreateCustomTimeZone(timeZoneId, offset, timeZoneId, timeZoneId);
            }

            // 4. Fallback por defecto: Colombia (UTC-5)
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Bogota");
            }
            catch
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("SA Pacific Standard Time");
                }
                catch
                {
                    return TimeZoneInfo.CreateCustomTimeZone("COT", TimeSpan.FromHours(-5), "Colombia Standard Time (UTC-5)", "Colombia Standard Time");
                }
            }
        }

        private static string? MapIanaOrWindowsId(string id) => id switch
        {
            "America/Bogota" => "SA Pacific Standard Time",
            "SA Pacific Standard Time" => "America/Bogota",
            "America/New_York" => "Eastern Standard Time",
            "Eastern Standard Time" => "America/New_York",
            "America/Mexico_City" => "Central Standard Time (Mexico)",
            "Central Standard Time (Mexico)" => "America/Mexico_City",
            "America/Argentina/Buenos_Aires" => "Argentina Standard Time",
            "Argentina Standard Time" => "America/Argentina/Buenos_Aires",
            "Europe/Madrid" => "Romance Standard Time",
            "Romance Standard Time" => "Europe/Madrid",
            "UTC" => "UTC",
            _ => null
        };

        /// <summary>
        /// Obtiene la fecha y hora actual en la zona horaria configurada (por defecto Colombia, UTC-5).
        /// </summary>
        public static DateTime GetLocalNow(TimeZoneInfo? customTz = null)
        {
            var tz = customTz ?? _currentTimeZone;
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        }

        /// <summary>
        /// Formatea una fecha UTC en la zona horaria configurada con su offset correspondiente.
        /// Ej: "2026-11-11 10:15:30.123 -05:00"
        /// </summary>
        public static string FormatLocalTime(DateTime? utcDateTime = null, TimeZoneInfo? customTz = null)
        {
            var tz = customTz ?? _currentTimeZone;
            var utc = utcDateTime ?? DateTime.UtcNow;
            if (utc.Kind == DateTimeKind.Unspecified)
            {
                utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            }
            else if (utc.Kind == DateTimeKind.Local)
            {
                utc = utc.ToUniversalTime();
            }

            var localTime = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
            var offset = tz.GetUtcOffset(localTime);
            var offsetStr = (offset >= TimeSpan.Zero ? "+" : "-") + offset.ToString(@"hh\:mm");

            return $"{localTime:yyyy-MM-dd HH:mm:ss.fff} {offsetStr}";
        }

        // --- Métodos de compatibilidad con código existente ---
        public static DateTime GetColombiaNow() => GetLocalNow();
        public static string FormatColombiaTime(DateTime? utcDateTime = null) => FormatLocalTime(utcDateTime);
    }
}
