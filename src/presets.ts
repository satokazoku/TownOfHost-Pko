// ===== プリセット共有API =====
// 実際のスキーマ(src/db/migrations/001_create_presets.sql)に合わせています:
//   presets(id INTEGER PK AUTOINCREMENT, name, creator, config_json, version,
//           tags TEXT(カンマ区切り), downloads, created_at)
// JSON側の形(name/uploaderName/data/tags配列/downloadCount/createdAt)は
// C#モジュール(PresetShareClient.cs)側を変更せずに済むよう、そのまま維持しています。
// description列は無いので受け取っても保存しません(将来列を足せば繋がります)。

export interface Env {
    DB: D1Database;
    RELAY_SECRET?: string;
}

const MAX_NAME_LENGTH = 100;
const MAX_UPLOADER_NAME_LENGTH = 50;
const MAX_VERSION_LENGTH = 30;
const MAX_TAG_LENGTH = 20;
const MAX_TAGS_PER_PRESET = 10;
const MAX_DATA_LENGTH = 500_000;
const DEFAULT_PAGE_SIZE = 20;
const MAX_PAGE_SIZE = 50;

const CORS_HEADERS = {
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Headers": "Content-Type, X-Relay-Secret",
    "Access-Control-Allow-Methods": "GET, POST, OPTIONS",
};

function json(data: unknown, status = 200): Response {
    return new Response(JSON.stringify(data), {
        status,
        headers: { "Content-Type": "application/json", ...CORS_HEADERS },
    });
}

function checkSecret(request: Request, env: Env): boolean {
    if (!env.RELAY_SECRET || env.RELAY_SECRET === "none") return true;
    return request.headers.get("X-Relay-Secret") === env.RELAY_SECRET;
}

// タグはカンマ区切りの1列に "先頭・末尾にもカンマを付けた形" で正規化して保存し、
// 検索時は ",tag," を部分一致させることで「人」が「人外」に誤ヒットするのを防ぐ。
function normalizeTagsForStorage(tags: string[]): string {
    const cleaned = tags.map((t) => t.trim()).filter((t) => t.length > 0);
    return cleaned.length > 0 ? cleaned.join(",") : "";
}

function parseStoredTags(tagsRaw: string | null): string[] {
    if (!tagsRaw) return [];
    return tagsRaw.split(",").map((t) => t.trim()).filter((t) => t.length > 0);
}

interface PresetRow {
    id: number;
    name: string;
    creator: string;
    version: string;
    tags: string | null;
    downloads: number;
    created_at: string;
}

function rowToSummary(r: PresetRow) {
    return {
        id: String(r.id),
        name: r.name,
        uploaderName: r.creator,
        description: "", // 列が無いので常に空文字
        version: r.version ?? "",
        tags: parseStoredTags(r.tags),
        downloadCount: r.downloads,
        createdAt: r.created_at,
    };
}

// ===== POST /presets : アップロード =====
export async function handlePresetUpload(request: Request, env: Env): Promise<Response> {
    if (!checkSecret(request, env)) return json({ error: "Unauthorized" }, 401);

    let body: any;
    try {
        body = await request.json();
    } catch {
        return json({ error: "Invalid JSON" }, 400);
    }

    const name = String(body?.name ?? "").trim();
    const uploaderName = String(body?.uploaderName ?? "Unknown").trim() || "Unknown";
    const version = String(body?.version ?? "").trim();
    const data = String(body?.data ?? ""); // config_json列へ
    const tagsRaw: unknown = body?.tags ?? [];

    if (!name) return json({ error: "name is required" }, 400);
    if (!data) return json({ error: "data is required" }, 400);
    if (name.length > MAX_NAME_LENGTH) return json({ error: `name must be ${MAX_NAME_LENGTH} chars or less` }, 400);
    if (uploaderName.length > MAX_UPLOADER_NAME_LENGTH) return json({ error: "uploaderName too long" }, 400);
    if (version.length > MAX_VERSION_LENGTH) return json({ error: "version too long" }, 400);
    if (data.length > MAX_DATA_LENGTH) return json({ error: `data too large (max ${MAX_DATA_LENGTH} bytes)` }, 400);

    if (!Array.isArray(tagsRaw)) return json({ error: "tags must be an array" }, 400);
    const tags = [...new Set(tagsRaw.map((t) => String(t).trim()).filter((t) => t.length > 0))];
    if (tags.length > MAX_TAGS_PER_PRESET) return json({ error: `too many tags (max ${MAX_TAGS_PER_PRESET})` }, 400);
    if (tags.some((t) => t.length > MAX_TAG_LENGTH)) return json({ error: `tag too long (max ${MAX_TAG_LENGTH} chars)` }, 400);

    const tagsForStorage = normalizeTagsForStorage(tags);

    const result = await env.DB.prepare(
        `INSERT INTO presets (name, creator, config_json, version, tags, downloads)
     VALUES (?, ?, ?, ?, ?, 0) RETURNING id`
    ).bind(name, uploaderName, data, version || "1", tagsForStorage).first<{ id: number }>();

    return json({ id: String(result?.id ?? ""), success: true });
}

// ===== GET /presets : 一覧(検索・タグ絞り込み・バージョン絞り込み・並び替え・ページング) =====
export async function handlePresetList(request: Request, env: Env, url: URL): Promise<Response> {
    const search = (url.searchParams.get("search") ?? "").trim();
    const version = (url.searchParams.get("version") ?? "").trim();
    const tagsParam = (url.searchParams.get("tags") ?? "").trim();
    const tags = tagsParam ? tagsParam.split(",").map((t) => t.trim()).filter(Boolean) : [];
    const sort = url.searchParams.get("sort") === "popular" ? "downloads" : "created_at";
    const page = Math.max(1, parseInt(url.searchParams.get("page") ?? "1", 10) || 1);
    const pageSize = Math.min(
        MAX_PAGE_SIZE,
        Math.max(1, parseInt(url.searchParams.get("pageSize") ?? String(DEFAULT_PAGE_SIZE), 10) || DEFAULT_PAGE_SIZE)
    );
    const offset = (page - 1) * pageSize;

    const conditions: string[] = [];
    const bindings: unknown[] = [];

    if (search) {
        conditions.push("name LIKE ?");
        bindings.push(`%${search}%`);
    }
    if (version) {
        conditions.push("version = ?");
        bindings.push(version);
    }
    if (tags.length > 0) {
        // カンマ区切り列に対して ",tag," の部分一致でOR検索(部分文字列誤爆を回避)
        const tagConds = tags.map(() => `(',' || tags || ',') LIKE ?`);
        conditions.push(`(${tagConds.join(" OR ")})`);
        for (const t of tags) bindings.push(`%,${t},%`);
    }

    const whereClause = conditions.length > 0 ? `WHERE ${conditions.join(" AND ")}` : "";

    const countRow = await env.DB.prepare(`SELECT COUNT(*) as total FROM presets ${whereClause}`)
        .bind(...bindings)
        .first<{ total: number }>();

    const listResult = await env.DB.prepare(
        `SELECT id, name, creator, version, tags, downloads, created_at
     FROM presets
     ${whereClause}
     ORDER BY ${sort} DESC
     LIMIT ? OFFSET ?`
    )
        .bind(...bindings, pageSize, offset)
        .all<PresetRow>();

    return json({
        presets: (listResult.results ?? []).map(rowToSummary),
        totalCount: countRow?.total ?? 0,
        page,
        pageSize,
    });
}

// ===== GET /presets/:id : ダウンロード(取得と同時にdownloads+1) =====
export async function handlePresetDownload(request: Request, env: Env, idParam: string): Promise<Response> {
    const id = parseInt(idParam, 10);
    if (!Number.isFinite(id)) return json({ error: "Invalid id" }, 400);

    const row = await env.DB.prepare(
        `SELECT id, name, creator, config_json, version, tags, downloads, created_at FROM presets WHERE id = ?`
    )
        .bind(id)
        .first<PresetRow & { config_json: string }>();

    if (!row) return json({ error: "Not found" }, 404);

    await env.DB.prepare(`UPDATE presets SET downloads = downloads + 1 WHERE id = ?`).bind(id).run();

    return json({
        ...rowToSummary(row),
        data: row.config_json,
        downloadCount: row.downloads + 1,
    });
}

// ===== GET /presets/tags : 使われているタグ一覧(件数付き) =====
export async function handlePresetTagList(env: Env): Promise<Response> {
    const rows = await env.DB.prepare(`SELECT tags FROM presets WHERE tags IS NOT NULL AND tags != ''`).all<{
        tags: string;
    }>();

    const counts = new Map<string, number>();
    for (const r of rows.results ?? []) {
        for (const tag of parseStoredTags(r.tags)) {
            counts.set(tag, (counts.get(tag) ?? 0) + 1);
        }
    }

    const tags = [...counts.entries()]
        .map(([tag, count]) => ({ tag, count }))
        .sort((a, b) => b.count - a.count);

    return json({ tags });
}

// ===== ルーティング本体 =====
export async function handlePresetRequest(request: Request, env: Env): Promise<Response | null> {
    const url = new URL(request.url);

    if (request.method === "OPTIONS" && url.pathname.startsWith("/presets")) {
        return new Response(null, { status: 204, headers: CORS_HEADERS });
    }
    if (url.pathname === "/presets" && request.method === "POST") {
        return handlePresetUpload(request, env);
    }
    if (url.pathname === "/presets" && request.method === "GET") {
        return handlePresetList(request, env, url);
    }
    if (url.pathname === "/presets/tags" && request.method === "GET") {
        return handlePresetTagList(env);
    }
    const downloadMatch = url.pathname.match(/^\/presets\/(\d+)$/);
    if (downloadMatch && request.method === "GET") {
        return handlePresetDownload(request, env, downloadMatch[1]);
    }

    return null;
}