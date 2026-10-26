using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OxiDock.Services
{
    public class LocalAiAssistantServer
    {
        private HttpListener? _listener;
        private bool _isRunning;
        private readonly Func<object> _getStatusCallback;
        private readonly Action<string, JsonElement> _executeActionCallback;

        public LocalAiAssistantServer(Func<object> getStatus, Action<string, JsonElement> executeAction)
        {
            _getStatusCallback = getStatus;
            _executeActionCallback = executeAction;
        }

        public void Start(int port = 48199)
        {
            if (_isRunning) return;

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();
                _isRunning = true;
                _ = ListenLoopAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OxiDock AI Server] Başlatılamadı: {ex.Message}");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { }
        }

        private async Task ListenLoopAsync()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch
                {
                    if (!_isRunning) break;
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;

            // CORS başlıkları (Yerel AI ve geliştirici araçları için)
            res.Headers.Add("Access-Control-Allow-Origin", "*");
            res.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            try
            {
                string path = req.Url?.AbsolutePath.ToLowerInvariant() ?? "/";

                if (req.HttpMethod == "GET" && (path == "/" || path == "/status" || path == "/api/status"))
                {
                    var status = _getStatusCallback();
                    string json = JsonSerializer.Serialize(status, new JsonSerializerOptions { WriteIndented = true });
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentLength64 = buffer.Length;
                    await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else if (req.HttpMethod == "GET" && path == "/mcp")
                {
                    var mcpMeta = new
                    {
                        name = "oxidock-assistant",
                        version = "2.0.0",
                        description = "OxiDock Local AI Assistant MCP Server",
                        tools = new[]
                        {
                            new { name = "inspect_dock", description = "OxiDock'un aktif profili, widget listesi ve ayarlarını inceler." },
                            new { name = "switch_profile", description = "Dock profilini değiştirir (örn: 'Standart', 'İş & Geliştirici', 'Odaklanma')." },
                            new { name = "toggle_widget", description = "Saat, hava durumu, sistem, müzik, pomodoro veya not widget'ını açıp kapatır." },
                            new { name = "control_pomodoro", description = "Pomodoro odak sayacını başlatır, durdurur veya sıfırlar." }
                        }
                    };
                    string json = JsonSerializer.Serialize(mcpMeta, new JsonSerializerOptions { WriteIndented = true });
                    byte[] buffer = Encoding.UTF8.GetBytes(json);
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentLength64 = buffer.Length;
                    await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                }
                else if (req.HttpMethod == "POST")
                {
                    using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
                    string body = await reader.ReadToEndAsync();
                    JsonElement payload = default;
                    if (!string.IsNullOrWhiteSpace(body))
                    {
                        body = body.Trim();
                        JsonDocument? doc = null;
                        try
                        {
                            doc = JsonDocument.Parse(body);
                        }
                        catch
                        {
                            string cleaned = body.Replace(@"\", "");
                            if (cleaned.StartsWith("\"") && cleaned.EndsWith("\"") && cleaned.Length > 2)
                            {
                                cleaned = cleaned.Substring(1, cleaned.Length - 2);
                            }
                            doc = JsonDocument.Parse(cleaned);
                        }
                        using (doc)
                        {
                            payload = doc.RootElement.Clone();
                        }
                    }

                    _executeActionCallback(path, payload);

                    byte[] resp = Encoding.UTF8.GetBytes("{\"success\":true,\"message\":\"OxiDock action applied\"}");
                    res.ContentType = "application/json; charset=utf-8";
                    res.ContentLength64 = resp.Length;
                    await res.OutputStream.WriteAsync(resp, 0, resp.Length);
                }
                else
                {
                    res.StatusCode = 404;
                }
            }
            catch (Exception ex)
            {
                res.StatusCode = 500;
                byte[] err = Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message}\"}}");
                await res.OutputStream.WriteAsync(err, 0, err.Length);
            }
            finally
            {
                res.Close();
            }
        }
    }
}
