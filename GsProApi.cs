using System;
using OpenSkyPlusApi;

namespace GSPro4OSP;

internal static class GsProApi
{
    private static Action<string, LogLevels> _log;
    private static GsPro4Osp _launchMonitorApi;
    public static bool InMatch { get; private set; }

    public static async void Initialize(GsPro4Osp launchMonitorApi)
    {
        _log = GsPro4Osp.Logger;
        _launchMonitorApi = launchMonitorApi;
        ConnectionManager.MessageConnected += RespondToConnect;
        await ConnectionManager.Connect();
    }

    public static void OnResponse(GsProResponse response)
    {
        try
        {
            var responseCode = response?.Code ?? 0;

            switch (responseCode)
            {
                case 201:
                    if ((response?.Player?.DistanceToTarget ?? 0) != 0)
                    {
                        CalculatePuttingMode(response.Player?.Club, response.Player?.DistanceToTarget ?? 0);
                        _launchMonitorApi.ReadyForNextShot();
                    }

                    break;
                case 202:
                    InMatch = true;
                    break;
                case 203:
                    if ((response?.Message ?? "") == "GSPro round ended")
                        InMatch = false;
                    break;
                default:
                    if ((response?.Code ?? 500) >= 500)
                        _log($"Received a failure response from GSPro:\nCode: {response?.Code}\n" +
                             $"Message: {response?.Message}", LogLevels.Warning);
                    break;
            }
        }
        catch (Exception ex)
        {
            _log($"Failed when processing response:\n{ex}", LogLevels.Debug);
        }
    }

    private static void CalculatePuttingMode(string club, float distanceToPin)
    {
        try
        {
            var distanceToPtMode = ConfigurationManager.Configuration.DistanceToPtMode;
            var shotMode = _launchMonitorApi.GetShotMode();

            switch (distanceToPtMode)
            {
                case -1:
                    break;
                case 0:
                    if (club == "PT" || club == "LW" || club == "SW")
                    {
                        if (shotMode == ShotMode.Normal) _launchMonitorApi.SetPuttingMode();
                    }
                    else
                    {
                        if (shotMode == ShotMode.Putting) _launchMonitorApi.SetNormalMode();
                    }

                    break;
                default:
                    if (distanceToPin != 0)
                    {
                        if (distanceToPin <= distanceToPtMode || club == "PT")
                        {
                            if (shotMode == ShotMode.Normal) _launchMonitorApi.SetPuttingMode();
                        }
                        else
                        {
                            if (shotMode == ShotMode.Putting) _launchMonitorApi.SetNormalMode();
                        }
                    }

                    break;
            }
        }
        catch (Exception ex)
        {
            _log($"Calculate putting mode failed:\n{ex}", LogLevels.Debug);
        }
    }

    private static void RespondToConnect()
    {
        if (InMatch)
            _launchMonitorApi.ReadyForNextShot();
    }

    public static async void SendStatus(bool ready)
    {
        var request = new GsProRequest
        {
            ShotDataOptions = new ShotDataOptions
            {
                IsHeartBeat = false
            }
        };
        request.ShotDataOptions.IsHeartBeat = false;
        request.ShotDataOptions.LaunchMonitorIsReady = ready;
        await ConnectionManager.Send(request);
    }

    public static async void SendShotData(ShotData shotData)
    {
        GsProRequest request = new();
        ClubData clubData = new();
        BallData ballData = new();

        clubData.Speed = shotData.Club.HeadSpeed * 2.23694f; // m/s to mph
        ballData.HLA = shotData.Launch.HorizontalAngle;
        ballData.VLA = shotData.Launch.LaunchAngle;
        ballData.Speed = shotData.Launch.TotalSpeed * 2.23694f; // m/s to mph
        _log($"Adjusting shot speed from {shotData.Launch.TotalSpeed}m/s to {ballData.Speed}mph", LogLevels.Debug);
        ballData.BackSpin = shotData.Spin.Backspin;
        ballData.SideSpin = shotData.Spin.SideSpin;
        ballData.TotalSpin = shotData.Spin.TotalSpin;
        ballData.SpinAxis = shotData.Spin.SpinAxis;

        request.ShotNumber = ShotCounter.Shot;
        request.ClubData = clubData;
        request.BallData = ballData;
        request.ShotDataOptions.ContainsBallData = true;
        request.ShotDataOptions.ContainsClubData = true;
        request.ShotDataOptions.IsHeartBeat = false;

        await ConnectionManager.Send(request);
    }
}

public class GsProResponse
{
    public int? Code { get; set; } = null;
    public string Message { get; set; } = null;
    public PlayerData Player { get; set; } = null;
}

public class PlayerData
{
    public string Handed { get; set; } = null;
    public string Club { get; set; } = null;
    public float? DistanceToTarget { get; set; } = null;
    public string Surface { get; set; } = null;
}

public class GsProRequest
{
    public string APIversion = "1";

    public GsProRequest(bool heartBeat)
    {
        ShotDataOptions.IsHeartBeat = heartBeat;
    }

    public GsProRequest()
    {
    }

    public string DeviceID { get; set; } = "GsPro4Osp";
    public string Units { get; set; } = "Yards";
    public int ShotNumber { get; set; } = ShotCounter.Shot;
    public BallData BallData { get; set; }
    public ClubData ClubData { get; set; }
    public ShotDataOptions ShotDataOptions { get; set; } = new();
}

public class BallData
{
    public float? Speed { get; set; }
    public float? SpinAxis { get; set; }
    public float? TotalSpin { get; set; }
    public float? BackSpin { get; set; } // required if TotalSpin missing
    public float? SideSpin { get; set; } // required if TotalSpin missing
    public float? HLA { get; set; }
    public float? VLA { get; set; }
    public float? CarryDistance { get; set; } = null; // optional
}

public class ClubData
{
    public float? Speed { get; set; }
    public float? AngleOfAttack { get; set; } = null;
    public float? FaceToTarget { get; set; } = null;
    public float? Lie { get; set; } = null;
    public float? Loft { get; set; } = null;
    public float? Path { get; set; } = null;
    public float? SpeedAtImpact { get; set; } = null;
    public float? VerticalFaceImpact { get; set; } = null;
    public float? HorizontalFaceImpact { get; set; } = null;
    public float? ClosureRate { get; set; } = null;
}

public class ShotDataOptions
{
    public bool ContainsBallData { get; set; } // required
    public bool ContainsClubData { get; set; } // required
    public bool? LaunchMonitorIsReady { get; set; }
    public bool? LaunchMonitorBallDetected { get; set; } = null;
    public bool? IsHeartBeat { get; set; } = true;
}