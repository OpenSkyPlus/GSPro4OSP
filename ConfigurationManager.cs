using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using OpenSkyPlusApi;

namespace GSPro4OSP;

internal static class ConfigurationManager
{
    public static Action<string, LogLevels> Log;
    public static PluginSettings Configuration;

    static ConfigurationManager()
    {
        Log = GsPro4Osp.Logger;
        var whereAmI = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;

        Configuration = new PluginSettings();
        new ConfigurationBuilder()
            .SetBasePath(whereAmI)
            .AddJsonFile("pluginsettings.json", false, true)
            .Build().GetSection("PluginSettings").Bind(Configuration);
    }
}

public class PluginSettings
{
    public string Hostname { get; set; }
    public int Port { get; set; }
    public int MaxRetries { get; set; } // -1 = forever
    public int RetryDelay { get; set; } // in ms

    // Distance in yards for the monitor to switch to putting mode.
    // 0: switch based on club
    // -1: never switch
    public float DistanceToPtMode { get; set; }

    /// <summary>
    /// When switching based on clubs, this list is used to switch to Putting mode
    /// </summary>
    public string[] PuttingModeClubs { get; set; }
}