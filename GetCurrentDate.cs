using System;

class GetCurrentDate
{
    static void Main()
    {
        // Get current UTC time
        DateTime utcNow = DateTime.UtcNow;

        // Attempt to get Stockholm time zone (Windows and Linux identifiers)
        TimeZoneInfo stockholmTimeZone = null;
        try
        {
            // Windows identifier
            stockholmTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central European Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
            try
            {
                // Linux/macOS identifier
                stockholmTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Stockholm");
            }
            catch (TimeZoneNotFoundException)
            {
                Console.Error.WriteLine("Could not find Stockholm time zone on this system.");
                return;
            }
        }
        catch (InvalidTimeZoneException ex)
        {
            Console.Error.WriteLine($"Invalid time zone data: {ex.Message}");
            return;
        }

        // Convert UTC to Stockholm local time
        DateTime stockholmTime = TimeZoneInfo.ConvertTimeFromUtc(utcNow, stockholmTimeZone);

        // Output formatted strings
        string utcFormatted = utcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        string stockholmFormatted = stockholmTime.ToString("yyyy-MM-dd HH:mm:ss 'Sweden'");

        Console.WriteLine($"Current UTC time: {utcFormatted}");
        Console.WriteLine($"Current Sweden time: {stockholmFormatted}");
    }
}
