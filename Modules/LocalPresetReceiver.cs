using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace TownOfHost
{
    public static class LocalPresetReceiver
    {
        private static HttpListener _listener;
        private static Thread _listenerThread;
        private static bool _running = false;

        public static void Start(string port)
        {
            if (_running) return;
            if (!int.TryParse(port, out var p)) p = 50080;
            var prefix = $"http://127.0.0.1:{p}/";
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(prefix);
                _listener.Start();
                _running = true;
                _listenerThread = new Thread(() => Run(_listener));
                _listenerThread.IsBackground = true;
                _listenerThread.Start();
                Logger.Info($"LocalPresetReceiver started on {prefix}", "LocalPresetReceiver");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "LocalPresetReceiver");
            }
        }

        public static void Stop()
        {
            try
            {
                _running = false;
                _listener?.Stop();
                _listenerThread = null;
                Logger.Info("LocalPresetReceiver stopped", "LocalPresetReceiver");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "LocalPresetReceiver");
            }
        }

        private static void Run(HttpListener listener)
        {
            while (_running)
            {
                try
                {
                    var context = listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException) { break; }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "LocalPresetReceiver");
                }
            }
        }

        private static void HandleRequest(HttpListenerContext ctx)
        {
            try
            {
                var req = ctx.Request;
                var resp = ctx.Response;
                if (req.HttpMethod != "POST" || req.Url.AbsolutePath != "/load-preset")
                {
                    resp.StatusCode = 404;
                    resp.Close();
                    return;
                }

                using var ms = new MemoryStream();
                req.InputStream.CopyTo(ms);
                var body = Encoding.UTF8.GetString(ms.ToArray());

                // Save the received preset to a file for now
                var safeName = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var outPath = Path.Combine(Main.BaseDirectory, $"loaded_preset_{safeName}.json");
                try
                {
                    if (!Directory.Exists(Main.BaseDirectory)) Directory.CreateDirectory(Main.BaseDirectory);
                    File.WriteAllText(outPath, body, Encoding.UTF8);
                    Logger.Info($"Preset saved to {outPath}", "LocalPresetReceiver");
                    resp.StatusCode = 200;
                    var buff = Encoding.UTF8.GetBytes("ok");
                    resp.OutputStream.Write(buff, 0, buff.Length);
                    resp.Close();
                }
                catch (Exception ex)
                {
                    Logger.Exception(ex, "LocalPresetReceiver");
                    resp.StatusCode = 500;
                    resp.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "LocalPresetReceiver");
            }
        }
    }
}
