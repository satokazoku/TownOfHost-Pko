using System;
using System.Collections.Concurrent;
using HarmonyLib;
using InnerNet;

namespace TownOfHost.Modules;

// HTTP通信(バックグラウンドスレッド)からUnityのGameObject操作(メインスレッドのみ安全)へ
// 結果を安全に受け渡すためのキュー。DiscordMatchmakingRelayServiceのTick()と同じ
// InnerNetClient.FixedUpdateフックで、キューに溜まったActionを毎フレーム消化する。
public static class MainThreadDispatcher
{
    static readonly ConcurrentQueue<Action> Queue = new();

    // どのスレッドからでも安全に呼べる
    public static void Enqueue(Action action)
    {
        if (action == null) return;
        Queue.Enqueue(action);
    }

    // メインスレッドからのみ呼ぶこと(Harmonyパッチ経由でのみ呼ばれる想定)
    public static void DrainQueue()
    {
        // 1回のフレームで詰まりすぎないよう上限を設ける
        const int maxPerTick = 20;
        var processed = 0;
        while (processed < maxPerTick && Queue.TryDequeue(out var action))
        {
            try { action(); }
            catch (Exception e) { Logger.Exception(e, nameof(MainThreadDispatcher)); }
            processed++;
        }
    }
}

[HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.FixedUpdate))]
internal static class MainThreadDispatcherTickPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        MainThreadDispatcher.DrainQueue();
    }
}