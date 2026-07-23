// ===== バグ報告API =====
// workers/src/index.ts から import して既存ルーターに接続してください。
// presets.tsと同じ自己完結スタイルにしています。

export interface Env {
    RELAY_SECRET?: string;
    BUG_REPORT_WEBHOOK_URL?: string; // Discordの「バグ報告」チャンネル用Webhook URL(要設定)
}

const MAX_DETAILS_LENGTH = 1000; // Discord embed の1フィールド上限(1024)に収める
const MAX_SHORT_FIELD_LENGTH = 100;
const DISCORD_EMBED_COLOR = 0xE74C3C; // 赤系

function json(data: unknown, status = 200): Response {
    return new Response(JSON.stringify(data), {
        status,
        headers: {
            "Content-Type": "application/json",
            "Access-Control-Allow-Origin": "*",
            "Access-Control-Allow-Headers": "Content-Type, X-Relay-Secret",
            "Access-Control-Allow-Methods": "POST, OPTIONS",
        },
    });
}

function checkSecret(request: Request, env: Env): boolean {
    if (!env.RELAY_SECRET || env.RELAY_SECRET === "none") return true;
    return request.headers.get("X-Relay-Secret") === env.RELAY_SECRET;
}

function truncate(s: string, max: number): string {
    return s.length <= max ? s : s.slice(0, max - 1) + "…";
}

export async function handleBugReportSubmit(request: Request, env: Env): Promise<Response> {
    if (!checkSecret(request, env)) return json({ error: "Unauthorized" }, 401);

    if (!env.BUG_REPORT_WEBHOOK_URL || env.BUG_REPORT_WEBHOOK_URL === "none") {
        return json({ error: "BUG_REPORT_WEBHOOK_URL is not configured on the server" }, 500);
    }

    let body: any;
    try {
        body = await request.json();
    } catch {
        return json({ error: "Invalid JSON" }, 400);
    }

    const modVersion = truncate(String(body?.modVersion ?? "不明"), MAX_SHORT_FIELD_LENGTH);
    const amongUsVersion = truncate(String(body?.amongUsVersion ?? "不明"), MAX_SHORT_FIELD_LENGTH);
    const gameState = truncate(String(body?.gameState ?? "不明"), MAX_SHORT_FIELD_LENGTH);
    const reporterName = truncate(String(body?.reporterName ?? "Unknown"), MAX_SHORT_FIELD_LENGTH);
    const roomCode = truncate(String(body?.roomCode ?? ""), MAX_SHORT_FIELD_LENGTH);
    const details = String(body?.details ?? "").trim();

    if (!details) return json({ error: "details is required" }, 400);
    const truncatedDetails = truncate(details, MAX_DETAILS_LENGTH);

    const fields: { name: string; value: string; inline?: boolean }[] = [
        { name: "①Modバージョン", value: modVersion, inline: true },
        { name: "②Among Usバージョン", value: amongUsVersion, inline: true },
        { name: "状況(ロビー/会議/タスク等)", value: gameState, inline: true },
        { name: "報告者", value: reporterName, inline: true },
    ];
    if (roomCode) fields.push({ name: "部屋コード", value: roomCode, inline: true });
    fields.push({ name: "③〜⑥ 詳細(何が起きたか/いつ誰が何を/何試合目・何ターン目/実際の動作)", value: truncatedDetails });

    const payload = {
        embeds: [
            {
                title: "🐛 バグ報告",
                color: DISCORD_EMBED_COLOR,
                fields,
                timestamp: new Date().toISOString(),
            },
        ],
    };

    try {
        const res = await fetch(env.BUG_REPORT_WEBHOOK_URL, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload),
        });

        if (!res.ok) {
            const errBody = await res.text();
            return json({ error: `Discord webhook failed: ${res.status} ${errBody}` }, 502);
        }

        return json({ success: true });
    } catch (e) {
        return json({ error: `Failed to send: ${e instanceof Error ? e.message : String(e)}` }, 500);
    }
}

export async function handleBugReportRequest(request: Request, env: Env): Promise<Response | null> {
    const url = new URL(request.url);

    if (request.method === "OPTIONS" && url.pathname === "/bugreport") {
        return new Response(null, {
            status: 204,
            headers: {
                "Access-Control-Allow-Origin": "*",
                "Access-Control-Allow-Headers": "Content-Type, X-Relay-Secret",
                "Access-Control-Allow-Methods": "POST, OPTIONS",
            },
        });
    }

    if (url.pathname === "/bugreport" && request.method === "POST") {
        return handleBugReportSubmit(request, env);
    }

    return null;
}