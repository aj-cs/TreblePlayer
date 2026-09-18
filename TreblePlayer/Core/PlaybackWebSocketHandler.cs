using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace TreblePlayer.Core;

public class PlaybackWebSocketHandler
{
    private readonly MusicPlayer _player;
    private readonly ConcurrentDictionary<string, WebSocket> _sockets = new();
    private readonly SemaphoreSlim _broadcastLock = new(1, 1);
    private readonly ILogger<PlaybackWebSocketHandler> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public PlaybackWebSocketHandler(MusicPlayer player, ILogger<PlaybackWebSocketHandler> logger)
    {
        _player = player;
        _logger = logger;

        // Hook into MusicPlayer events
        _player.PlaybackStarted += trackId => Broadcast(new { type = "PlaybackStarted", trackId });
        _player.PlaybackPaused += () => Broadcast(new { type = "PlaybackPaused" });
        _player.PlaybackStopped += () => Broadcast(new { type = "PlaybackStopped" });
        _player.PlaybackResumed += () => Broadcast(new { type = "PlaybackResumed" });
        _player.PlaybackSeeked += seconds => Broadcast(new { type = "PlaybackSeeked", seconds });
        _player.PositionChanged += seconds => Broadcast(new { type = "PositionChanged", seconds });
    }

    public async Task HandleAsync(HttpContext context, WebSocket webSocket)
    {
        var socketId = Guid.NewGuid().ToString();
        _sockets.TryAdd(socketId, webSocket);
        _logger.LogInformation($"WebSocket connection established: {socketId}");

        try
        {
            var buffer = new byte[1024 * 4];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                }
                // We don't expect messages from client for now, as commands go through controllers
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"WebSocket error for {socketId}: {ex.Message}");
        }
        finally
        {
            _sockets.TryRemove(socketId, out _);
            _logger.LogInformation($"WebSocket connection closed: {socketId}");
        }
    }

    private void Broadcast(object message)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        _ = BroadcastAsync(bytes);
    }

    private async Task BroadcastAsync(byte[] bytes)
    {
        await _broadcastLock.WaitAsync();
        try
        {
            foreach (var socket in _sockets.Values)
            {
                if (socket.State != WebSocketState.Open) continue;

                try
                {
                    await socket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"Failed to send WebSocket message: {ex.Message}");
                }
            }
        }
        finally
        {
            _broadcastLock.Release();
        }
    }

    // Additional broadcast methods for other events can be added here
    public void BroadcastNotification(string type, object? data = null)
    {
        Broadcast(new { type, data });
    }
}
