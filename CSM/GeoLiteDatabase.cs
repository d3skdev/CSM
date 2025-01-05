using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using System;
using System.IO;
using System.Reflection;

public class GeoLiteDatabase
{
    private static DatabaseReader _reader; // Make it static
    private static readonly object _lock = new object(); // For thread safety

    // Static constructor to initialize the reader ONCE
    static GeoLiteDatabase()
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string resourceName = "CSM.GeoLite2-Country.mmdb";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new Exception($"Embedded resource '{resourceName}' not found.");
                }

                string tempFilePath = Path.GetTempFileName();
                using (FileStream fileStream = new FileStream(tempFilePath, FileMode.Create))
                {
                    stream.CopyTo(fileStream);
                }

                try
                {
                    _reader = new DatabaseReader(tempFilePath);
                }
                finally
                {
                    // Only delete the temp file if the reader was successfully created.
                    if (_reader == null && File.Exists(tempFilePath))
                    {
                        File.Delete(tempFilePath);
                    }
                }

            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing GeoLite database: {ex.Message}");
            _reader = null; // Important
        }
    }

    public static string GetCountryName(string ipAddress, bool isIso = false)
    {
        if (_reader == null)
            return null;

        lock (_lock) // Thread safety
        {
            try
            {
                var response = _reader.Country(ipAddress);
                if (isIso)
                    return response.Country.IsoCode;
                else
                    return response.Country.Name;
            }
            catch (AddressNotFoundException)
            {
                return "N/A";
            }
            catch (Exception ex)
            {
                //System.Diagnostics.Debug.WriteLine($"Error looking up IP: {ex.Message}");
                return "N/A";
            }
        }
    }

    // Call this only ONCE when your application shuts down.
    public static void Close()
    {
        if (_reader != null)
        {
            _reader.Dispose();
            _reader = null; // Important to prevent potential issues if Close() is called multiple times.
        }
    }
}