using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LogLevel = BepInEx.Logging.LogLevel;
using TownOfHost.Modules;

namespace TownOfHost
{
    class Webhook
    {
        private static readonly HttpClient HttpClient = new();

        public static bool SendToUrl(string text, string webhookUrl)
        {
            if (Main.IsAndroid()) return false;
            if (string.IsNullOrWhiteSpace(webhookUrl) || webhookUrl == "none") return false;
            HttpClient httpClient = new();
            Dictionary<string, string> strs = new()
            {
                { "content", text },
            };
            try
            {
                TaskAwaiter<HttpResponseMessage> awaiter = httpClient.PostAsync(
                    webhookUrl, new FormUrlEncodedContent(strs)).GetAwaiter();
                awaiter.GetResult();
                return true;
            }
            catch
            {
                Logger.Warn("WebHookの送信に失敗", nameof(Webhook));
                return false;
            }
        }

        public static void Send(string text)
        {
            if (Main.IsAndroid()) return;
            ClientOptionsManager.CheckOptions();
            if (ClientOptionsManager.WebhookUrl == "none" || !Main.UseWebHook.Value) return;
            _ = SendToUrl(text, ClientOptionsManager.WebhookUrl);
        }
        //参考元→https://github.com/Dolly1016/Nebula-Public/
        public static void SendResult(byte[] pngImage)
        {
            if (Main.IsAndroid()) return;
            ClientOptionsManager.CheckOptions();
            if (ClientOptionsManager.WebhookUrl == "none" || !Main.UseWebHook.Value) return;

            try
            {
                var webhookUrl = ClientOptionsManager.WebhookUrl;
                var threadName = $"{DateTime.Now:yyyy年 MM月dd日 HH:mm} {Main.GameCount}試合目";
                var killLog = UtilsGameLog.BuildKillLogText().RemoveHtmlTags();
                var preset = ClientOptionsManager.PresetData
                    ? Encoding.UTF8.GetBytes(OptionSerializer.GenerateOptionsString())
                    : null;
                var currentLog = ClientOptionsManager.LogData
                    ? UtilsOutputLog.ReadCurrentLog()
                    : null;
                var gameCount = Main.GameCount;

                _ = Task.Run(() => SendGameResultToForum(
                    webhookUrl,
                    threadName,
                    pngImage,
                    killLog,
                    preset,
                    currentLog,
                    gameCount));
            }
            catch (Exception e)
            {
                Logger.Info($"{e}", "SendResult");
            }
        }

        private static void SendGameResultToForum(
            string webhookUrl,
            string threadName,
            byte[] pngImage,
            string killLog,
            byte[] preset,
            byte[] currentLog,
            int gameCount)
        {
            try
            {
                var createUrl = AddQuery(webhookUrl, "wait=true");
                var createPayload = $"{{\"thread_name\":\"{EscapeJson(threadName)}\",\"allowed_mentions\":{{\"parse\":[]}}}}";
                var responseBody = Post(createUrl, createPayload, pngImage, "image.png");
                var match = Regex.Match(responseBody, "\\\"channel_id\\\"\\s*:\\s*\\\"(?<id>\\d+)\\\"");
                if (!match.Success)
                    throw new InvalidOperationException("Discord response did not contain the created forum thread ID.");

                var threadUrl = AddQuery(webhookUrl, $"thread_id={match.Groups["id"].Value}");
                foreach (var part in SplitMessage(killLog))
                {
                    var payload = $"{{\"content\":\"{EscapeJson(part)}\",\"allowed_mentions\":{{\"parse\":[]}}}}";
                    Post(threadUrl, payload);
                }

                if (preset != null)
                {
                    Post(
                        threadUrl,
                        "{\"allowed_mentions\":{\"parse\":[]}}",
                        preset,
                        $"Preset_Data.txt");
                }
                if (currentLog != null)
                {
                    Post(
                        threadUrl,
                        "{\"allowed_mentions\":{\"parse\":[]}}",
                        currentLog,
                        $"TOHP_AutoLog_{gameCount}試合目.txt");
                }
            }
            catch (Exception e)
            {
                Logger.Info($"{e}", "SendResult");
            }
        }

        private static string Post(string url, string payload, byte[] file = null, string fileName = null)
        {
            for (var attempt = 0; attempt < 4; attempt++)
            {
                using MultipartFormDataContent content = new();
                content.Add(new StringContent(payload, Encoding.UTF8, "application/json"), "payload_json");
                if (file != null)
                    content.Add(new ByteArrayContent(file), "files[0]", fileName);

                using var response = HttpClient.PostAsync(url, content).GetAwaiter().GetResult();
                var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (response.IsSuccessStatusCode) return responseBody;

                if ((int)response.StatusCode == 429 && attempt < 3)
                {
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(1);
                    Thread.Sleep(retryAfter + TimeSpan.FromMilliseconds(100));
                    continue;
                }

                throw new HttpRequestException($"Discord webhook returned {(int)response.StatusCode}: {responseBody}");
            }

            throw new HttpRequestException("Discord webhook retry limit was exceeded.");
        }

        private static IEnumerable<string> SplitMessage(string text, int maxLength = 2000)
        {
            if (string.IsNullOrEmpty(text)) yield break;

            var offset = 0;
            while (offset < text.Length)
            {
                var length = Math.Min(maxLength, text.Length - offset);
                if (offset + length < text.Length)
                {
                    var newline = text.LastIndexOf('\n', offset + length - 1, length);
                    if (newline >= offset)
                        length = newline - offset + 1;
                }

                yield return text.Substring(offset, length);
                offset += length;
            }
        }

        private static string AddQuery(string url, string query) =>
            $"{url}{(url.Contains('?') ? '&' : '?')}{query}";

        private static string EscapeJson(string value)
        {
            var builder = new StringBuilder(value.Length + 16);
            foreach (var character in value)
            {
                switch (character)
                {
                    case '\\': builder.Append("\\\\"); break;
                    case '"': builder.Append("\\\""); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < ' ')
                            builder.Append($"\\u{(int)character:x4}");
                        else
                            builder.Append(character);
                        break;
                }
            }
            return builder.ToString();
        }
    }

    class Alert
    {
        /*
        public static void Send(string text, string name = "TownOfHost-Pko", string avatar = "https://cdn.discordapp.com/attachments/1219855613752774657/1254725875535183933/TabIcon_MainSettings.png?ex=667a8a08&is=66793888&hm=dc20a50c7cadab0a15a215c19abcde6006fbef9911299ab82e452b7cf5242f57&")
        {
            ClientOptionsManager.CheckOptions();
            HttpClient httpClient = new();
            Dictionary<string, string> strs = new()
            {
                { "content", text },
                { "username", name },
                { "avatar_url", avatar }
            };
            TaskAwaiter<HttpResponseMessage> awaiter = httpClient.PostAsync(
                Main.DebugwebURL, new FormUrlEncodedContent(strs)).GetAwaiter();
            awaiter.GetResult();
        }*/
    }

    class Logger
    {
        public static bool isEnable;
        public static List<string> disableList = new();
        public static List<string> sendToGameList = new();
        public static bool isDetail = false;
        public static bool isAlsoInGame = false;
        public static void Enable() => isEnable = true;
        public static void Disable() => isEnable = false;
        public static void Enable(string tag, bool toGame = false)
        {
            disableList.Remove(tag);
            if (toGame && !sendToGameList.Contains(tag)) sendToGameList.Add(tag);
            else sendToGameList.Remove(tag);
        }
        public static void Disable(string tag) { if (!disableList.Contains(tag)) disableList.Add(tag); }
        public static void seeingame(string text, bool isAlways = false)
        {
            if (!isEnable) return;
            if (DestroyableSingleton<HudManager>._instance) DestroyableSingleton<HudManager>.Instance.Notifier.AddDisconnectMessage(text);
        }
        private static void SendToFile(string text, LogLevel level = LogLevel.Info, string tag = "", bool escapeCRLF = true, int lineNumber = 0, string fileName = "")
        {
            if (!isEnable || disableList.Contains(tag)) return;
            var logger = Main.Logger;
            string t = DateTime.Now.ToString("HH:mm:ss:ff");
            if (sendToGameList.Contains(tag) || isAlsoInGame) seeingame($"[{tag}]{text}");
            if (escapeCRLF)
                text = text.Replace("\r", "\\r").Replace("\n", "\\n");
            string log_text = $"[{t}][{tag}]{text}";
            if (isDetail && DebugModeManager.AmDebugger)
            {
                StackFrame stack = new(2);
                string className = stack.GetMethod().ReflectedType.Name;
                string memberName = stack.GetMethod().Name;
                log_text = $"[{t}][{className}.{memberName}({Path.GetFileName(fileName)}:{lineNumber})][{tag}]{text}";
            }
            switch (level)
            {
                case LogLevel.Info:
                    logger.LogInfo(log_text);
                    break;
                case LogLevel.Warning:
                    logger.LogWarning(log_text);
                    break;
                case LogLevel.Error:
                    logger.LogError(log_text);
                    break;
                case LogLevel.Fatal:
                    logger.LogFatal(log_text);
                    break;
                case LogLevel.Message:
                    logger.LogMessage(log_text);
                    break;
                default:
                    logger.LogWarning("Error:Invalid LogLevel");
                    logger.LogInfo(log_text);
                    break;
            }
        }
        public static void Info(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "") =>
            SendToFile(text, LogLevel.Info, tag, escapeCRLF, lineNumber, fileName);
        public static void Warn(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "") =>
            SendToFile(text, LogLevel.Warning, tag, escapeCRLF, lineNumber, fileName);
        public static void Error(string text, string tag, bool escapeCRLF = false, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "") =>
            SendToFile(text, LogLevel.Error, tag, escapeCRLF, lineNumber, fileName);
        public static void Fatal(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "") =>
            SendToFile(text, LogLevel.Fatal, tag, escapeCRLF, lineNumber, fileName);
        public static void Msg(string text, string tag, bool escapeCRLF = true, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "") =>
            SendToFile(text, LogLevel.Message, tag, escapeCRLF, lineNumber, fileName);
        public static void Exception(Exception ex, string tag, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "") =>
            SendToFile(ex.ToString(), LogLevel.Error, tag, false, lineNumber, fileName);
        static float OldDate = -1;
        public static void CheckElapsed(string tag)
        {
            var Nowdate = DateTime.Now;
            var NowTime = Nowdate.Millisecond + Nowdate.Second * 1000 + Nowdate.Minute * 100000 + Nowdate.Hour * 10000000;
            var elapsed = OldDate < 0 ? "null" : $"{NowTime - OldDate}";

            SendToFile($"{DateTime.Now:HH.mm.ss.fff} ({elapsed})", LogLevel.Info, tag);
            OldDate = Nowdate.Millisecond + Nowdate.Second * 1000 + Nowdate.Minute * 100000 + Nowdate.Hour * 10000000;
        }
        public static void CurrentMethod([CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "")
        {
            StackFrame stack = new(1);
            Logger.Msg($"\"{stack.GetMethod().ReflectedType.Name}.{stack.GetMethod().Name}\" Called in \"{Path.GetFileName(fileName)}({lineNumber})\"", "Method");
        }

        public static LogHandler Handler(string tag, bool escapeCRLF = true)
            => new(tag, escapeCRLF);
    }
}
