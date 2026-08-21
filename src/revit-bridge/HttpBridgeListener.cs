using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace RevitBridge
{
    public class HttpBridgeListener
    {
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private readonly ExternalEvent _externalEvent;
        private readonly RevitExternalHandler _externalHandler;
        private const string Prefix = "http://localhost:8000/";

        public HttpBridgeListener(ExternalEvent externalEvent, RevitExternalHandler externalHandler)
        {
            _externalEvent = externalEvent ?? throw new ArgumentNullException(nameof(externalEvent));
            _externalHandler = externalHandler ?? throw new ArgumentNullException(nameof(externalHandler));
        }

        public void Start()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(Prefix);
                _listener.Start();

                _cts = new CancellationTokenSource();

                Task.Run(() => ListenAsync(_cts.Token));
            }
            catch (Exception ex)
            {
                TaskDialog.Show("RevitBridge Error", $"Failed to start HTTP Listener on {Prefix}: {ex.Message}");
            }
        }

        public void Stop()
        {
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
            }
            catch
            {
                // Ignore cleanup errors on shutdown
            }
        }

        private async Task ListenAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = ProcessRequestAsync(context);
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[RevitBridge] HTTP Request error: {ex.Message}");
                }
            }
        }

        private async Task ProcessRequestAsync(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");
            response.ContentType = "application/json; charset=utf-8";

            if (request.HttpMethod.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase))
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.Close();
                return;
            }

            string responseJson;
            int statusCode = (int)HttpStatusCode.OK;

            try
            {
                var rawPath = request.Url?.AbsolutePath.ToLowerInvariant() ?? "/";
                var method = request.HttpMethod.ToUpperInvariant();

                if (method == "GET" && rawPath == "/health")
                {
                    responseJson = await DispatchToRevitAsync(TaskType.HealthCheck);
                }
                else if (method == "GET" && rawPath == "/selection")
                {
                    responseJson = await DispatchToRevitAsync(TaskType.GetSelection);
                }
                else if (method == "POST" && rawPath == "/selection/set")
                {
                    string body = await ReadBodyAsync(request);
                    responseJson = await DispatchToRevitAsync(TaskType.SelectElements, body);
                }
                else if (method == "POST" && rawPath == "/element/parameters")
                {
                    string body = await ReadBodyAsync(request);
                    responseJson = await DispatchToRevitAsync(TaskType.GetElementParameters, body);
                }
                else if (method == "POST" && rawPath == "/element/info")
                {
                    string body = await ReadBodyAsync(request);
                    responseJson = await DispatchToRevitAsync(TaskType.GetElementInfo, body);
                }
                else if (method == "POST" && rawPath == "/elements/by-category")
                {
                    string body = await ReadBodyAsync(request);
                    responseJson = await DispatchToRevitAsync(TaskType.GetElementsByCategory, body);
                }
                else if (method == "POST" && rawPath == "/parameter/set")
                {
                    string body = await ReadBodyAsync(request);
                    responseJson = await DispatchToRevitAsync(TaskType.SetParameter, body);
                }
                else if (method == "GET" && rawPath == "/project/info")
                {
                    responseJson = await DispatchToRevitAsync(TaskType.GetProjectInfo);
                }
                else if (method == "GET" && rawPath == "/levels")
                {
                    responseJson = await DispatchToRevitAsync(TaskType.GetLevels);
                }
                else if (method == "GET" && rawPath == "/views")
                {
                    responseJson = await DispatchToRevitAsync(TaskType.GetViews);
                }
                else if (method == "POST" && rawPath == "/elements/delete")
                {
                    string body = await ReadBodyAsync(request);
                    responseJson = await DispatchToRevitAsync(TaskType.DeleteElements, body);
                }
                else
                {
                    statusCode = (int)HttpStatusCode.NotFound;
                    responseJson = "{\"status\":\"error\",\"message\":\"Endpoint not found.\"}";
                }
            }
            catch (TimeoutException)
            {
                statusCode = (int)HttpStatusCode.RequestTimeout;
                responseJson = "{\"status\":\"error\",\"message\":\"Request timeout waiting for Revit UI thread execution.\"}";
            }
            catch (Exception ex)
            {
                statusCode = (int)HttpStatusCode.InternalServerError;
                responseJson = $"{{\"status\":\"error\",\"message\":\"Internal server error: {JsonEscape(ex.Message)}\"}}";
            }

            try
            {
                response.StatusCode = statusCode;
                byte[] buffer = Encoding.UTF8.GetBytes(responseJson);
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                response.OutputStream.Close();
            }
            catch
            {
                // Client disconnected
            }
        }

        private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
        {
            using var reader = new StreamReader(request.InputStream, request.ContentEncoding);
            return await reader.ReadToEndAsync();
        }

        private async Task<string> DispatchToRevitAsync(TaskType type, string? payloadJson = null)
        {
            var task = new BridgeTask
            {
                Type = type,
                PayloadJson = payloadJson
            };

            _externalHandler.EnqueueTask(task);
            _externalEvent.Raise();

            // Wait up to 15 seconds for Revit main UI thread execution
            var completedTask = await Task.WhenAny(task.TaskCompletionSource.Task, Task.Delay(15000));
            if (completedTask == task.TaskCompletionSource.Task)
            {
                return await task.TaskCompletionSource.Task;
            }

            throw new TimeoutException();
        }

        private static string JsonEscape(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", "\\n");
        }
    }
}
