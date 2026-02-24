using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClipNotes.Services;

public class ObsWebSocketService : IDisposable
{
    private ClientWebSocket? _ws;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private int _requestId;
    private readonly Dictionary<string, TaskCompletionSource<JsonNode?>> _pendingRequests = new();
    private readonly object _lock = new();

    public event Action<string>? RecordStateChanged;
    public event Action<string>? RecordStopped; // passes outputPath
    public event Action<string>? Error;
    public bool IsConnected => _ws?.State == WebSocketState.Open;

    public async Task<bool> ConnectAsync(string host, int port, string? password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            Error?.Invoke("OBS host не может быть пустым");
            return false;
        }
        try
        {
            Disconnect();
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            var uri = new Uri($"ws://{host}:{port}");
            await _ws.ConnectAsync(uri, ct);

            // Read Hello message
            var hello = await ReceiveMessageAsync(ct);
            if (hello == null) return false;

            var helloData = hello["d"];
            var authRequired = helloData?["authentication"] != null;

            // Build Identify
            var identify = new JsonObject
            {
                ["op"] = 1,
                ["d"] = new JsonObject
                {
                    ["rpcVersion"] = 1,
                    ["eventSubscriptions"] = 1 << 6 // Outputs events
                }
            };

            if (authRequired && password != null)
            {
                var challenge = helloData!["authentication"]!["challenge"]!.GetValue<string>();
                var salt = helloData!["authentication"]!["salt"]!.GetValue<string>();
                var auth = GenerateAuth(password, salt, challenge);
                identify["d"]!["authentication"] = auth;
            }
            else if (authRequired)
            {
                Error?.Invoke("OBS требует пароль, но он не указан");
                return false;
            }

            await SendAsync(identify, ct);

            // Read Identified
            var identified = await ReceiveMessageAsync(ct);
            if (identified == null || identified["op"]?.GetValue<int>() != 2)
            {
                Error?.Invoke("Ошибка аутентификации OBS WebSocket");
                return false;
            }

            // Start receive loop
            _receiveTask = Task.Run(() => ReceiveLoop(_cts.Token));
            return true;
        }
        catch (Exception ex)
        {
            Error?.Invoke($"Не удалось подключиться к OBS: {ex.Message}");
            return false;
        }
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        try { _ws?.Dispose(); } catch { }
        _ws = null;
        _cts = null;
    }

    public async Task<JsonNode?> SendRequestAsync(string requestType, JsonObject? requestData = null, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _requestId).ToString();
        var tcs = new TaskCompletionSource<JsonNode?>();

        lock (_lock) _pendingRequests[id] = tcs;

        var msg = new JsonObject
        {
            ["op"] = 6,
            ["d"] = new JsonObject
            {
                ["requestType"] = requestType,
                ["requestId"] = id,
                ["requestData"] = requestData ?? new JsonObject()
            }
        };

        await SendAsync(msg, ct);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);
        // Clean up the pending entry on cancellation/timeout to prevent dictionary memory leak
        linked.Token.Register(() =>
        {
            lock (_lock) _pendingRequests.Remove(id);
            tcs.TrySetCanceled();
        });

        return await tcs.Task;
    }

    public async Task StartRecordAsync(CancellationToken ct = default)
    {
        await SendRequestAsync("StartRecord", null, ct);
    }

    public async Task<string?> StopRecordAsync(CancellationToken ct = default)
    {
        var result = await SendRequestAsync("StopRecord", null, ct);
        return result?["outputPath"]?.GetValue<string>();
    }

    public async Task SetRecordDirectoryAsync(string directory, CancellationToken ct = default)
    {
        await SendRequestAsync("SetRecordDirectory", new JsonObject
        {
            ["recordDirectory"] = directory
        }, ct);
    }

    public async Task<(TimeSpan duration, string timecode, bool isRecording)?> GetRecordStatusAsync(CancellationToken ct = default)
    {
        var result = await SendRequestAsync("GetRecordStatus", null, ct);
        if (result == null) return null;

        var active = result["outputActive"]?.GetValue<bool>() ?? false;
        var durationStr = result["outputDuration"]?.ToString() ?? "0";
        var timecode = result["outputTimecode"]?.GetValue<string>() ?? "00:00:00.000";

        double durationMs = 0;
        if (double.TryParse(durationStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var d))
            durationMs = d;

        return (TimeSpan.FromMilliseconds(durationMs), timecode, active);
    }

    private async Task SendAsync(JsonObject msg, CancellationToken ct)
    {
        if (_ws?.State != WebSocketState.Open) return;
        var json = msg.ToJsonString();
        var bytes = Encoding.UTF8.GetBytes(json);
        await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private async Task<JsonNode?> ReceiveMessageAsync(CancellationToken ct)
    {
        if (_ws == null) return null;
        var buffer = new byte[65536];
        using var ms = new MemoryStream();
        WebSocketReceiveResult result;
        do
        {
            result = await _ws.ReceiveAsync(buffer, ct);
            ms.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);

        var json = Encoding.UTF8.GetString(ms.ToArray());
        return JsonNode.Parse(json);
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _ws?.State == WebSocketState.Open)
            {
                var msg = await ReceiveMessageAsync(ct);
                if (msg == null) continue;

                var op = msg["op"]?.GetValue<int>() ?? 0;

                if (op == 7) // RequestResponse
                {
                    var id = msg["d"]?["requestId"]?.GetValue<string>();
                    if (id != null)
                    {
                        TaskCompletionSource<JsonNode?>? tcs;
                        lock (_lock)
                        {
                            _pendingRequests.TryGetValue(id, out tcs);
                            if (tcs != null) _pendingRequests.Remove(id);
                        }
                        tcs?.TrySetResult(msg["d"]?["responseData"]);
                    }
                }
                else if (op == 5) // Event
                {
                    var eventType = msg["d"]?["eventType"]?.GetValue<string>();
                    if (eventType == "RecordStateChanged")
                    {
                        var state = msg["d"]?["eventData"]?["outputState"]?.GetValue<string>() ?? "";
                        RecordStateChanged?.Invoke(state);

                        if (state == "OBS_WEBSOCKET_OUTPUT_STOPPED")
                        {
                            var path = msg["d"]?["eventData"]?["outputPath"]?.GetValue<string>() ?? "";
                            RecordStopped?.Invoke(path);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
        catch (Exception ex)
        {
            Error?.Invoke($"OBS WebSocket ошибка: {ex.Message}");
        }
    }

    private static string GenerateAuth(string password, string salt, string challenge)
    {
        var secret = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(password + salt)));
        var auth = Convert.ToBase64String(
            SHA256.HashData(Encoding.UTF8.GetBytes(secret + challenge)));
        return auth;
    }

    public void Dispose()
    {
        Disconnect();
    }
}
