using System;
using OpenSkyPlusApi;

namespace GSPro4OSP;

public static class ShotCounter
{
    public static int Shot { get; private set; }

    public static void IncShot()
    {
        Shot++;
    }
}

public class GsPro4Osp : AbstractOpenSkyPlusApi
{
    public static GsPro4Osp Instance;
    public new static string PluginName = "GsPro4Osp";

    protected override void Initialize()
    {
        Instance = this;
        ConnectionManager.Initialize();
        GsProApi.Initialize(this);
        OnShot += HandleShot;
        OnReady += () => { HandleStatus(null, true); };
        OnNotReady += () => { HandleStatus(null, false); };
        OnConnect += () => { HandleStatus(true); };
        OnDisconnect += () => { HandleStatus(false); };
    }

    public static void Logger(string message, LogLevels level)
    {
        Instance?.LogToOpenSkyPlus($"{PluginName}> {message}", level);
    }

    public void HandleStatus(bool? connected = null, bool? ready = null)
    {
        try
        {
            var isConnected = connected ?? IsConnected();
            var isReady = ready ?? IsReady();

            if (isConnected && isReady)
                GsProApi.SendStatus(true);
            else
                GsProApi.SendStatus(false);
        }
        catch (Exception ex)
        {
            Logger($"Problem with setting ready/connected state\n{ex}", LogLevels.Warning);
        }
    }

    public static void HandleShot()
    {
        try
        {
            ShotCounter.IncShot();
            GsProApi.SendShotData(Instance.GetLastShot());
        }
        catch (Exception ex)
        {
            Logger($"Failed to process the incoming Shot from the monitor:\n{ex}", LogLevels.Warning);
        }
    }
}