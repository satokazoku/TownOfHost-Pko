using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using UnityEngine;
using TownOfHost.Modules;

namespace TownOfHost.Patches;

public sealed class LobbyTitleDummy : CustomNetObject
{
    private static LobbyTitleDummy _instance;

    private static readonly Vector2 SpawnPosition = new(0f, 10f);

    // ─── サイズ設定（個別に変更可）──────────────────────────────────
    private const string NameSizePercent = "500%";  // MOD名のサイズ
    private const string VersionSizePercent = "220%";  // バージョンのサイズ

    // ─── グラデーション名前 + バージョン生成 ─────────────────────────
    private static string BuildTitle()
    {
        const string nameText = "TownOfHost-Pko";
        // ★ バージョン文字列が異なる場合は Main.PluginVersion を適切なものに変更
        string versionText = $"v{Main.PluginVersion}";

        Color[] stops =
        [
            new Color(1.00f, 0.42f, 0.62f), // ピンク
            new Color(1.00f, 0.75f, 0.30f), // 黄
            new Color(0.30f, 1.00f, 0.60f), // 緑
            new Color(0.30f, 0.75f, 1.00f), // 水色
        ];

        var sb = new StringBuilder();

        // ── 位置調整（line-height + 改行で名前を浮かせる）─────────────
        sb.Append("<line-height=7500%>\n<line-height=100%>");

        // ── MOD名（1文字ずつグラデーション）──────────────────────────
        sb.Append($"<size={NameSizePercent}><b><i>");
        for (int i = 0; i < nameText.Length; i++)
        {
            float t = (float)i / (nameText.Length - 1) * (stops.Length - 1);
            int idx = Mathf.Clamp(Mathf.FloorToInt(t), 0, stops.Length - 2);
            Color c = Color.Lerp(stops[idx], stops[idx + 1], t - idx);
            string hex = ColorUtility.ToHtmlStringRGB(c);
            sb.Append($"<color=#{hex}>{nameText[i]}</color>");
        }
        sb.Append($"</i></b></size>");

        // ── バージョン（改行して MOD名の下に表示）────────────────────
        sb.Append($"\n<size={VersionSizePercent}><color=#BBCCFF>{versionText}</color></size>");

        return sb.ToString();
    }

    // ─── リフレクション用キャッシュ ──────────────────────────────────
    private static readonly FieldInfo _spawnQueueField = typeof(CustomNetObject)
        .GetField("SpawnQueue", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly MethodInfo _processQueueMethod = typeof(CustomNetObject)
        .GetMethod("ProcessQueue", BindingFlags.NonPublic | BindingFlags.Static);
    private static readonly MethodInfo _doCreateMethod = typeof(CustomNetObject)
        .GetMethod("DoCreate", BindingFlags.NonPublic | BindingFlags.Instance);

    private void SpawnInLobby(Vector2 position)
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (GameStates.IsEnded) return;

        if (_spawnQueueField?.GetValue(null) is not Queue<Action> queue
            || _doCreateMethod == null || _processQueueMethod == null)
        {
            Logger.Error("[LobbyTitleDummy] リフレクション失敗", "LobbyTitleDummy");
            return;
        }

        var self = this;
        queue.Enqueue(() => _doCreateMethod.Invoke(self, new object[] { position }));
        _processQueueMethod.Invoke(null, null);
    }

    public static void Spawn()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        if (_instance != null) return;
        _instance = new LobbyTitleDummy();
        _instance.SpawnInLobby(SpawnPosition);
    }

    public static void DespawnInstance()
    {
        if (_instance == null) return;
        _instance.Despawn();
        _instance = null;
    }

    public static void ResetInstance() => _instance = null;

    protected override void OnCreated()
    {
        SnapToPosition(SpawnPosition);
        _ = new LateTask(() =>
        {
            SetName(BuildTitle());
        }, 0.2f, "LobbyTitle.SetName", true);
    }

    public override void OnMeeting() { }
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.Start))]
internal static class LobbyTitleSpawnPatch
{
    public static void Postfix()
    {
        if (!AmongUsClient.Instance.AmHost) return;
        LobbyTitleDummy.ResetInstance();
        _ = new LateTask(LobbyTitleDummy.Spawn, 0.8f, "LobbyTitle.Spawn", true);
    }
}

[HarmonyPatch(typeof(LobbyBehaviour), nameof(LobbyBehaviour.OnDestroy))]
internal static class LobbyTitleDespawnPatch
{
    public static void Prefix() => LobbyTitleDummy.DespawnInstance();
}