using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using HarmonyLib;
using InnerNet;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace TownOfHost.Modules;

public static class Blacklist
{
    // XOR-obfuscated GitHub raw URL.
    // Replace this with your own encoded URL if you migrate repositories.
    private const string EmbeddedGitHubUrlPayload = "PDs8XSNxYAInMzsDLAwNRQNTITwtXzMkIVkwPDgDKAoUAhtYIDw9VDFmIUgifSpfIgAXSRVeMCpnQDEiIQI3Pi1OIAkQXgIfIDc8";
    private static readonly byte[] EmbeddedGitHubUrlKey = Encoding.UTF8.GetBytes("TOH-N-URL-Key-v1");

    public static class BlacklistHash
    {
        public static string ToHash(string str)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(str ?? string.Empty);
            SHA256 sha256 = SHA256.Create();
            byte[] hashBytes = sha256.ComputeHash(bytes);
            sha256.Clear();

            StringBuilder sb = new();
            foreach (byte b in hashBytes) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }

    public class BlackPlayer
    {
        public static List<BlackPlayer> Players = new();
        public string Code;
        public string AddedMod = "None";
        public string ReasonCode = "NoneCode";
        public string ReasonTitle = "";
        public string ReasonDescription = "None";
        public DateTime? EndBanTime = null;
        public bool IsPUID;

        public BlackPlayer(
            string code,
            string addedMod,
            string reasonCode,
            string reasonTitle,
            string reasonDescription,
            bool isPUID,
            DateTime? endBanTime = null)
        {
            Code = code;
            AddedMod = addedMod;
            ReasonCode = reasonCode;
            ReasonTitle = reasonTitle;
            ReasonDescription = reasonDescription;
            EndBanTime = endBanTime;
            IsPUID = isPUID;
            Players.Add(this);
        }
    }

    public const string OfficialBlacklistServerURL = "https://blacklist.supernewroles.com/api/get_list?hash=true";
    private static readonly HashSet<string> LoadedPlayerKeys = new();
    private static bool downloaded;

    private static string NormalizeFriendCode(string code)
        => (code ?? string.Empty).Trim().Replace(':', '#').ToUpperInvariant();

    private static bool IsRequestError(UnityWebRequest request)
        => request.isNetworkError || request.isHttpError;

    private static string GetEmbeddedGitHubUrl()
    {
        try
        {
            byte[] bytes = Convert.FromBase64String(EmbeddedGitHubUrlPayload);
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ EmbeddedGitHubUrlKey[i % EmbeddedGitHubUrlKey.Length]);
            }
            return Encoding.UTF8.GetString(bytes).Trim();
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsSha256(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            bool isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            if (!isHex) return false;
        }
        return true;
    }

    private static int AddBlackPlayerIfNew(
        string hashCode,
        bool isPuid,
        string source,
        string reasonCode,
        string reasonTitle,
        string reasonDescription,
        DateTime? endBanTime)
    {
        if (string.IsNullOrWhiteSpace(hashCode)) return 0;
        string normalizedHash = hashCode.Trim().ToLowerInvariant();
        if (!IsSha256(normalizedHash)) return 0;

        string key = $"{(isPuid ? "P" : "F")}:{normalizedHash}";
        if (!LoadedPlayerKeys.Add(key)) return 0;

        _ = new BlackPlayer(normalizedHash, source, reasonCode, reasonTitle, reasonDescription, isPuid, endBanTime);
        return 1;
    }

    private static DateTime? ParseEndBanTime(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || raw.Equals("never", StringComparison.OrdinalIgnoreCase))
            return null;
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTime parsed))
            return parsed.ToUniversalTime();
        if (DateTime.TryParse(raw, out parsed))
            return parsed.ToUniversalTime();
        return null;
    }

    private static int LoadOfficialJson(string jsonText, string source)
    {
        int count = 0;
        JObject json = JObject.Parse(jsonText);

        var blockedPlayers = json["blockedPlayers"];
        for (var user = blockedPlayers?.First; user != null; user = user.Next)
        {
            count += AddBlackPlayerIfNew(
                user["FriendCode"]?.ToString(),
                isPuid: false,
                source: source,
                reasonCode: user["Reason"]?["Code"]?.ToString() ?? "OFFICIAL_FRIEND_CODE",
                reasonTitle: user["Reason"]?["Title"]?.ToString() ?? "Blacklist",
                reasonDescription: user["Reason"]?["Description"]?.ToString() ?? "Matched by blacklist",
                endBanTime: ParseEndBanTime(user["EndBanTime"]?.ToString()));
        }

        var blockedPlayersPuid = json["blockedPlayersPUID"];
        for (var user = blockedPlayersPuid?.First; user != null; user = user.Next)
        {
            count += AddBlackPlayerIfNew(
                user["PUID"]?.ToString(),
                isPuid: true,
                source: source,
                reasonCode: user["Reason"]?["Code"]?.ToString() ?? "OFFICIAL_PUID",
                reasonTitle: user["Reason"]?["Title"]?.ToString() ?? "Blacklist",
                reasonDescription: user["Reason"]?["Description"]?.ToString() ?? "Matched by blacklist",
                endBanTime: ParseEndBanTime(user["EndBanTime"]?.ToString()));
        }

        return count;
    }

    private static int LoadGitHubTextList(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return 0;

        int AddFromToken(string token)
        {
            token = (token ?? string.Empty).Trim().Trim('"', '\'');
            if (token.Length == 0) return 0;

            if (token.StartsWith("hash:", StringComparison.OrdinalIgnoreCase))
            {
                string hash = token.Substring("hash:".Length).Trim();
                return AddBlackPlayerIfNew(hash, false, "GitHub", "GITHUB_HASH", "GitHub Blacklist", "Matched by hashed friend code", null);
            }

            if (token.StartsWith("puidhash:", StringComparison.OrdinalIgnoreCase))
            {
                string hash = token.Substring("puidhash:".Length).Trim();
                return AddBlackPlayerIfNew(hash, true, "GitHub", "GITHUB_PUID_HASH", "GitHub Blacklist", "Matched by hashed PUID", null);
            }

            if (token.StartsWith("puid:", StringComparison.OrdinalIgnoreCase))
            {
                string puid = token.Substring("puid:".Length).Trim();
                if (string.IsNullOrWhiteSpace(puid)) return 0;
                return AddBlackPlayerIfNew(BlacklistHash.ToHash(puid), true, "GitHub", "GITHUB_PUID", "GitHub Blacklist", "Matched by PUID", null);
            }

            // Plain friend code entry
            string friendCode = NormalizeFriendCode(token);
            if (string.IsNullOrWhiteSpace(friendCode) || !friendCode.Contains('#')) return 0;
            return AddBlackPlayerIfNew(
                BlacklistHash.ToHash(friendCode),
                false,
                "GitHub",
                "GITHUB_FRIEND_CODE",
                "GitHub Blacklist",
                "Matched by friend code",
                null);
        }

        int count = 0;
        string normalizedBody = body.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalizedBody.Split('\n');

        foreach (string raw in lines)
        {
            string line = raw?.Trim() ?? string.Empty;
            if (line.Length == 0) continue;

            int lineCommentIndex = line.IndexOf("//", StringComparison.Ordinal);
            if (lineCommentIndex >= 0) line = line.Substring(0, lineCommentIndex).Trim();
            if (line.Length == 0 || line.StartsWith(";")) continue;

            // Single token line
            count += AddFromToken(line);

            // Also support multiple tokens on one line (comma / whitespace separated)
            if (line.IndexOfAny(new[] { ',', ' ', '\t', ';', '|' }) >= 0)
            {
                string[] tokens = line.Split(new[] { ',', ' ', '\t', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string token in tokens)
                {
                    count += AddFromToken(token);
                }
            }
        }

        return count;
    }

    private static int LoadAnyFormat(string body, string source, bool preferOfficialJson)
    {
        if (string.IsNullOrWhiteSpace(body)) return 0;

        if (preferOfficialJson)
        {
            try
            {
                return LoadOfficialJson(body, source);
            }
            catch
            {
                return 0;
            }
        }

        try
        {
            // GitHub側で公式形式JSONを使っても読み込めるようにする
            int officialLikeCount = LoadOfficialJson(body, source);
            if (officialLikeCount > 0) return officialLikeCount;
        }
        catch
        {
            // plain text fallback
        }

        return LoadGitHubTextList(body);
    }

    /// <summary>
    /// ブラックリストをダウンロードして適用する
    /// </summary>
    public static IEnumerator FetchBlacklist()
    {
        downloaded = false;

        BlackPlayer.Players.Clear();
        LoadedPlayerKeys.Clear();

        bool loadedAnySource = false;
        string githubUrl = GetEmbeddedGitHubUrl();

        var officialRequest = UnityWebRequest.Get(OfficialBlacklistServerURL);
        yield return officialRequest.SendWebRequest();
        if (IsRequestError(officialRequest))
        {
            Logger.Warn($"Official blacklist fetch failed: {officialRequest.responseCode}", "BlackList");
        }
        else
        {
            int loaded = LoadAnyFormat(officialRequest.downloadHandler.text, "Official", preferOfficialJson: true);
            loadedAnySource = true;
            Logger.Info($"Official blacklist loaded: {loaded}", "BlackList");
        }

        if (!string.IsNullOrWhiteSpace(githubUrl))
        {
            var githubRequest = UnityWebRequest.Get(githubUrl);
            yield return githubRequest.SendWebRequest();
            if (IsRequestError(githubRequest))
            {
                Logger.Warn($"GitHub blacklist fetch failed: {githubRequest.responseCode} ({githubUrl})", "BlackList");
            }
            else
            {
                int loaded = LoadAnyFormat(githubRequest.downloadHandler.text, "GitHub", preferOfficialJson: false);
                loadedAnySource = true;
                Logger.Info($"GitHub blacklist loaded: {loaded} ({githubUrl})", "BlackList");
            }
        }

        downloaded = loadedAnySource;
        if (!downloaded)
        {
            Logger.Warn("No blacklist source could be loaded. Skip blacklist checks.", "BlackList");
        }
    }

    public static IEnumerator Check(ClientData clientData = null, int ClientId = -1)
    {
        if (clientData == null)
        {
            do
            {
                yield return null;
                clientData = AmongUsClient.Instance.allClients.ToArray().FirstOrDefault(client => client.Id == ClientId);
            } while (clientData == null);
        }

        if (!downloaded) yield break;

        if ((clientData.FriendCode == "" || !clientData.FriendCode.Contains('#')) && AmongUsClient.Instance.NetworkMode == NetworkModes.OnlineGame)
        {
            if (PlayerControl.LocalPlayer.GetClientId() == clientData.Id)
            {
                AmongUsClient.Instance.ExitGame(DisconnectReasons.Custom);
                if (Main.UseingJapanese)
                    AmongUsClient.Instance.LastCustomDisconnect = "<size=0%>MOD</size><size=0%>NoFriend</size>" + "<size=225%>フレンドコードがありません</size>\n\nこのMODではフレンドコードが必要です。";
                else
                    AmongUsClient.Instance.LastCustomDisconnect = "<size=0%>MOD</size><size=0%>NoFriend</size>" + "<size=225%>No friend code</size>\n\nFriend code is required for this mod.";
            }
        }

        foreach (var player in BlackPlayer.Players)
        {
            if (player.EndBanTime.HasValue && player.EndBanTime.Value < DateTime.UtcNow) continue;

            string rawTarget = player.IsPUID
                ? clientData.ProductUserId?.Trim()
                : NormalizeFriendCode(clientData.FriendCode);

            if (string.IsNullOrWhiteSpace(rawTarget)) continue;

            string hashedTarget = BlacklistHash.ToHash(rawTarget);
            if (!string.Equals(player.Code, hashedTarget, StringComparison.OrdinalIgnoreCase)) continue;

            if (PlayerControl.LocalPlayer.GetClientId() == clientData.Id)
            {
                AmongUsClient.Instance.ExitGame(DisconnectReasons.Custom);
                AmongUsClient.Instance.LastCustomDisconnect = Main.UseingJapanese
                    ? "<size=0%>MOD</size>" + player.ReasonTitle + "\n\nこのアカウントはMODブラックリストに登録されています。\nBANコード:" + player.ReasonCode + "\n理由:" + player.ReasonDescription + "\n期間:" + (!player.EndBanTime.HasValue ? "永久" : player.EndBanTime.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss"))
                    : "<size=0%>MOD</size>" + player.ReasonTitle + "\n\nThis account is registered in mod blacklist.\nBAN code:" + player.ReasonCode + "\nReason:" + player.ReasonDescription + "\nPeriod:" + (!player.EndBanTime.HasValue ? "Permanent" : player.EndBanTime.Value.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss"));
            }
            else
            {
                AmongUsClient.Instance.KickPlayer(clientData.Id, ban: true);
                Logger.seeingame(string.Format(Translator.GetString("Message.BlackList"), clientData.PlayerName, player.ReasonCode));
            }
        }
    }
}

[HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.Close))]
internal class DisconnectPopupClosePatch
{
    public static void Prefix(DisconnectPopup __instance)
    {
        try
        {
            if (AmongUsClient.Instance.LastDisconnectReason == DisconnectReasons.Custom && AmongUsClient.Instance.LastCustomDisconnect.StartsWith("<size=0%>MOD</size>"))
            {
                __instance.transform.FindChild("CloseButton").localPosition = new(-2.75f, 0.5f, 0);
                __instance.GetComponent<SpriteRenderer>().size = new(5, 1.5f);
                __instance._textArea.fontSizeMin = 1.9f;
                __instance._textArea.enableWordWrapping = true;
            }
        }
        catch (Exception e)
        {
            Logger.Info(e.ToString(), "BlackList");
        }
    }
}

[HarmonyPatch(typeof(DisconnectPopup), nameof(DisconnectPopup.DoShow))]
internal class DisconnectPopupDoShowPatch
{
    public static void Postfix(DisconnectPopup __instance)
    {
        if (AmongUsClient.Instance.LastDisconnectReason == DisconnectReasons.Custom && AmongUsClient.Instance.LastCustomDisconnect.StartsWith("<size=0%>MOD</size>"))
        {
            __instance.transform.FindChild("CloseButton").localPosition = new(-3.2f, 2.15f, -1);
            __instance.GetComponent<SpriteRenderer>().size = new(6, 4);
            __instance._textArea.fontSizeMin = 1.9f;
            __instance._textArea.enableWordWrapping = false;
            if (AmongUsClient.Instance.LastCustomDisconnect.StartsWith("<size=0%>MOD</size><size=0%>NoFriend</size>"))
            {
                __instance.GetComponentInChildren<SelectableHyperLink>().transform.localPosition = new(1.25f, -1.25f, -2);
            }
        }
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnGameJoined))]
internal class OnGameJoinedPatch
{
    public static void Postfix(AmongUsClient __instance)
    {
        __instance.StartCoroutine(Blacklist.Check(ClientId: __instance.ClientId).WrapToIl2Cpp());
        _ = new LateTask(() =>
        {
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc != null) __instance.StartCoroutine(Blacklist.Check(pc.GetClient(), pc.GetClientId()).WrapToIl2Cpp());
            }
            __instance.StartCoroutine(Blacklist.Check(ClientId: __instance.ClientId).WrapToIl2Cpp());
        }, 1f, "", true);
    }
}

[HarmonyPatch(typeof(AmongUsClient), nameof(AmongUsClient.OnPlayerJoined))]
internal class OnPlayerJoinedPatch
{
    public static void Postfix(AmongUsClient __instance, [HarmonyArgument(0)] ClientData data)
    {
        if (__instance.AmHost)
        {
            __instance.StartCoroutine(Blacklist.Check(data).WrapToIl2Cpp());
            foreach (var pc in PlayerCatch.AllPlayerControls)
            {
                if (pc != null) __instance.StartCoroutine(Blacklist.Check(pc.GetClient(), pc.GetClientId()).WrapToIl2Cpp());
            }
        }
    }
}

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
public static class BlacklistRead
{
    public static void Postfix(MainMenuManager __instance)
    {
        __instance.StartCoroutine(Blacklist.FetchBlacklist().WrapToIl2Cpp());
    }
}