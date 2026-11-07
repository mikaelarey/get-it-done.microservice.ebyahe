using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;

namespace EByahe.Tests;

internal static class HubTestClient
{
  public static async Task<WebSocket> ConnectAsync(TestServer server, string path, string token)
  {
    var client = server.CreateWebSocketClient();
    client.ConfigureRequest = request => request.Headers["Origin"] = "http://localhost:4204";
    var socket = await client.ConnectAsync(new Uri("wss://localhost" + path + "?access_token=" + Uri.EscapeDataString(token)), CancellationToken.None);
    await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, CancellationToken.None);
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var handshake = await ReceiveAsync(socket, timeout.Token);
    Assert.Contains("{}", handshake);
    return socket;
  }

  public static async Task<string> ReceiveAsync(WebSocket socket, CancellationToken ct)
  {
    var data = new byte[16384];
    var text = new StringBuilder();
    do {
      var result = await socket.ReceiveAsync(data.AsMemory(), ct);
      if (result.MessageType == WebSocketMessageType.Close) throw new InvalidOperationException("Hub closed unexpectedly.");
      text.Append(Encoding.UTF8.GetString(data, 0, result.Count));
    } while (!text.ToString().EndsWith('\u001e'));
    return text.ToString();
  }

  public static async Task ExpectAsync(WebSocket socket, string method)
  {
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    while (true) {
      var message = await ReceiveAsync(socket, timeout.Token);
      if (message.Contains("\"target\":\"" + method + "\"", StringComparison.Ordinal)) return;
    }
  }
}
