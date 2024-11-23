using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OpenSkyPlusApi;

namespace GSPro4OSP;

public delegate void NotificationConnectionStatus();

internal static class ConnectionManager
{
    private static Action<string, LogLevels> _log;

    private static TcpClient _client;
    private static NetworkStream _stream;
    private static string Hostname { get; set; } = "127.0.0.1";
    private static int Port { get; set; } = 921;
    private static int MaxRetries { get; set; } = -1;
    private static int RetryDelay { get; set; } = 30000;

    public static event NotificationConnectionStatus MessageConnected;

    public static void Initialize()
    {
        _log = GsPro4Osp.Logger;
        var config = ConfigurationManager.Configuration;
        Hostname = config.Hostname;
        Port = config.Port;
        MaxRetries = config.MaxRetries;
        RetryDelay = config.RetryDelay;
        AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

        if (config.PuttingModeClubs == null)
        {
            _log($"PuttingModeClubs not specified in config, defaulting to \"PT\"", LogLevels.Warning);
            config.PuttingModeClubs = ["PT"];
        }
        _log($"DistanceToPtMode: {config.DistanceToPtMode}, PuttingModeClubs: {string.Join(",", config.PuttingModeClubs)}", LogLevels.Info);
    }

    public static async Task Connect()
    {
        var attempt = 1;
        while (attempt < MaxRetries || MaxRetries == -1)
            try
            {
                _log(
                    $"Connecting to GSPro API. {(MaxRetries == -1 ? "Will try indefinitely." : $"Attempt {attempt}/{MaxRetries}")}",
                    LogLevels.Info);
                _client = new TcpClient();
                await _client.ConnectAsync(Hostname, Port);
                _stream = _client.GetStream();
                MessageConnected?.Invoke();
                _log("Connected to GSPro!", LogLevels.Info);
                var heartbeat = new GsProRequest
                {
                    ShotDataOptions = new ShotDataOptions
                    {
                        LaunchMonitorIsReady = GsPro4Osp.Instance.IsReady()
                    }
                };
                await SendAsync(heartbeat);
                await ReadAsync();
                return;
            }
            catch (SocketException)
            {
                _log("Connection failed. Is GSPro running?", LogLevels.Info);
                attempt++;
                if (attempt < MaxRetries || MaxRetries == -1)
                {
                    _log($"Retrying in {RetryDelay / 1000} seconds.", LogLevels.Info);
                    await Task.Delay(RetryDelay);
                }
            }
            catch (Exception ex)
            {
                _log($"Unknown connection failure: {ex}", LogLevels.Warning);
            }

        _log("Exceeded max connection attempts. Giving up.", LogLevels.Info);
        _log("Could not connect to GSPro. Shot data will not be sent.", LogLevels.Warning);
    }

    public static async Task Send(GsProRequest request)
    {
        await SendAsync(request);
    }

    private static async Task SendAsync(GsProRequest request)
    {
        if (_stream == null || !(_client?.Connected ?? false)) return;

        var options = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore
        };

        var jsonRequest = JsonConvert.SerializeObject(request, options);

        var buffer = Encoding.UTF8.GetBytes(jsonRequest);
        try
        {
            _log($"\nSending request:\n{jsonRequest}", LogLevels.Debug);
            await _stream.WriteAsync(buffer, 0, buffer.Length);
        }
        catch (Exception ex)
        {
            _log($"Failed to send request to GSPro:\n{ex}", LogLevels.Warning);
        }
        finally
        {
            if (!_client.Connected)
                await Reconnect();
        }
    }

    private static async Task ReadAsync()
    {
        while (_client.Connected)
        {
            var jsonResponse = string.Empty;
            using var memoryStream = new MemoryStream();
            try
            {
                var receiveBuffer = new byte[4096];
                do
                {
                    var bytesRead = await _stream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);
                    if (bytesRead > 0) memoryStream.Write(receiveBuffer, 0, bytesRead);
                } while (_stream.DataAvailable);

                jsonResponse = Encoding.UTF8.GetString(memoryStream.ToArray());
                _log($"\nResponse:\n{jsonResponse}", LogLevels.Debug);

                using var stringReader = new StringReader(jsonResponse);
                using var jsonReader = new JsonTextReader(stringReader) { SupportMultipleContent = true };
                JsonSerializer serializer = new()
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore
                };
                List<GsProResponse> responses = [];
                while (await jsonReader.ReadAsync())
                    try
                    {
                        responses.Add(serializer.Deserialize<GsProResponse>(jsonReader));
                    }
                    catch (Exception ex)
                    {
                        _log($"Couldn't deserialize because {ex}", LogLevels.Debug);
                    }

                foreach (var response in responses)
                    GsProApi.OnResponse(response);
            }
            /*
             * This exception occurs when GSPro's API returns two json objects back
             * in the same response. This seems to only occur when a match/hole starts
             * and the ready message is sent with an immediate "match ended" message,
             * which we can assume is a bug. Since that is the only example we see,
             * we can treat this scenario like a match/hole start message
             */
            catch (JsonException)
            {
                if (jsonResponse.Contains("GSPro ready"))
                {
                    var readyResponse = new GsProResponse
                    {
                        Code = 202,
                        Message = "GSPro ready"
                    };
                    GsProApi.OnResponse(readyResponse);
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log($"GSPro responded unexpectedly:\n{ex}", LogLevels.Warning);
                await _stream.FlushAsync();
            }
        }

        await Reconnect();
    }

    private static async Task Reconnect()
    {
        try
        {
            _log("Connection to GSPro lost; Will attempt to reconnect.", LogLevels.Info);
            try
            {
                if (_client.Client.Connected)
                    _client.Client.Shutdown(SocketShutdown.Both);
                _stream.Dispose();
            }
            catch (Exception ex)
            {
                _log($"Couldn't gracefully close the socket connection. The server probably hung up.\n{ex}",
                    LogLevels.Debug);
            }
            finally
            {
                _client.Dispose();
                _stream = null;
                _client = null;
            }

            await Connect();
        }
        catch (Exception ex)
        {
            _log($"Error during reconnection:\n{ex}", LogLevels.Debug);
        }
    }

    public static void CurrentDomain_ProcessExit(object sender, EventArgs e)
    {
        _log("Shutting down.....", LogLevels.Info);
        _client.Client.Shutdown(SocketShutdown.Both);
        _client.Close();
        _stream.Flush();
        _stream.Dispose();
        _client.Dispose();
    }
}