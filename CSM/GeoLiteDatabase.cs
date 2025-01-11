using MaxMind.GeoIP2;
using MaxMind.GeoIP2.Exceptions;
using System;
using System.IO;
using System.Reflection;

public class GeoLiteDatabase
{
    private static DatabaseReader _countryReader;
    private static DatabaseReader _cityReader;
    private static DatabaseReader _asnReader;
    private static readonly object _lock = new object();

    static GeoLiteDatabase()
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            
            // Initialize all three readers
            _countryReader = InitializeReader("CSM.GeoLite2-Country.mmdb");
            _cityReader = InitializeReader("CSM.GeoLite2-City.mmdb");
            _asnReader = InitializeReader("CSM.GeoLite2-ASN.mmdb");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error initializing GeoLite databases: {ex.Message}");
            _countryReader = _cityReader = _asnReader = null;
        }
    }

    private static DatabaseReader InitializeReader(string resourceName)
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
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

            DatabaseReader reader = null;
            try
            {
                reader = new DatabaseReader(tempFilePath);
                return reader;
            }
            finally
            {
                // Clean up temp file if reader creation failed
                if (reader == null && File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }
    }

    // Example method for getting city information
    public static string? GetCityName(string ipAddress)
    {
        if (_cityReader == null)
            return null;

        lock (_lock)
        {
            try
            {
                var response = _cityReader.City(ipAddress);
                return string.IsNullOrEmpty(response.City.Name) ? null : response.City.Name;
            }
            catch (AddressNotFoundException)
            {
                return "N/A";
            }
            catch (Exception)
            {
                return "N/A";
            }
        }
    }

    // Example method for getting ASN information
    public static string? GetAsnInfo(string ipAddress)
    {
        if (_asnReader == null)
            return null;

        lock (_lock)
        {
            try
            {
                var response = _asnReader.Asn(ipAddress);
                return string.IsNullOrEmpty(response.AutonomousSystemOrganization) ? null : response.AutonomousSystemOrganization;

            }
            catch (AddressNotFoundException)
            {
                return "N/A";
            }
            catch (Exception)
            {
                return "N/A";
            }
        }
    }

    // Method for getting country information
    public static string GetCountryName(string ipAddress, bool isIso = false)
    {
        if (_countryReader == null)
            return null;

        lock (_lock)
        {
            try
            {
                                var response = _countryReader.Country(ipAddress);

                if (!isIso)
                      return response.Country.Name;
                else if (isIso)
                    return response.Country.IsoCode;
                else
                return response.Country.Name ?? "N/A";
            }
            catch (AddressNotFoundException)
            {
                return "N/A";
            }
            catch (Exception)
            {
                return "N/A";
            }
        }
    }

    // Updated Close method to handle all readers
    public static void Close()
    {
        _countryReader?.Dispose();
        _cityReader?.Dispose();
        _asnReader?.Dispose();
        
        _countryReader = _cityReader = _asnReader = null;
    }
}