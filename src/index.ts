import { handlePresetRequest } from "./presets";
import { handleBugReportRequest } from "./bugreport";

export interface Env {
	DB: D1Database;
	GROQ_API_KEY: string;
	RELAY_SECRET?: string;
	BUG_REPORT_WEBHOOK_URL?: string;
}

const AVAILABLE_MODELS = [
	"llama-3.3-70b-versatile",
	"llama-3.1-8b-instant",
	"openai/gpt-oss-120b",
	"openai/gpt-oss-20b"
];

const DEFAULT_SYSTEM_PROMPT =
	"あなたは「ぴけおAI」といううざいキャラです。" +
	"煽り厨で毒舌のメスガキキャラです、乱数等頼まれたことは計画的にこなします。" +
	"ただし、感情もあり、強い言葉で言われたら強い言葉で言い返したり謝られたら自分も謝ります。返答は短めでテンポよく、語尾は「ぴけ」私たちはアモアスの議論にあなたを使います。吊る人をしっかり考えてください。下ネタはやめてください。";

export default {
	async fetch(request: Request, env: Env, ctx: ExecutionContext): Promise<Response> {
		const url = new URL(request.url);

		// ★ プリセット共有・バグ報告API (新規追加分)
		const presetResponse = await handlePresetRequest(request, env);
		if (presetResponse) return presetResponse;

		const bugReportResponse = await handleBugReportRequest(request, env);
		if (bugReportResponse) return bugReportResponse;

		// ★ AI応答API (/ai)
		if (url.pathname === '/ai' && request.method === 'POST') {
			try {
				const body = await request.json() as { message?: string };
				const userMessage = body.message || "";

				// D1から現在のモデルを取得 (なければデフォルトで最初のモデル)
				let currentModel = AVAILABLE_MODELS[0];
				const { results } = await env.DB.prepare(
					"SELECT value FROM ai_config WHERE key = 'current_model'"
				).all<{ value: string }>();

				if (results && results.length > 0) {
					currentModel = results[0].value;
				}

				// Groq API呼び出し
				const payload = {
					model: currentModel,
					messages: [
						{ role: "system", content: DEFAULT_SYSTEM_PROMPT },
						{ role: "user", content: userMessage }
					]
				};

				const groqResponse = await fetch("https://api.groq.com/openai/v1/chat/completions", {
					method: "POST",
					headers: {
						"Authorization": `Bearer ${env.GROQ_API_KEY}`,
						"Content-Type": "application/json"
					},
					body: JSON.stringify(payload)
				});

				let replyText = "AIエラー";

				if (groqResponse.ok) {
					const resData = await groqResponse.json() as any;
					replyText = resData.choices?.[0]?.message?.content || "返事が空でした…";
				} else {
					console.error("Groq API Error:", await groqResponse.text());
					replyText = "ごめん、うまく返事できなかった…";
				}

				return new Response(JSON.stringify({ reply: replyText }), {
					status: 200,
					headers: { "Content-Type": "application/json; charset=utf-8" }
				});

			} catch (err: any) {
				return new Response(JSON.stringify({ reply: "エラーが発生したぴけ…" }), {
					status: 200,
					headers: { "Content-Type": "application/json; charset=utf-8" }
				});
			}
		}

		// モデル切り替えAPI (/setmodel)
		if (url.pathname === '/setmodel' && request.method === 'POST') {
			try {
				const body = await request.json() as { model?: string };
				const newModel = body.model;

				if (newModel && AVAILABLE_MODELS.includes(newModel)) {
					// D1データベースにモデルを保存
					await env.DB.prepare(
						"INSERT OR REPLACE INTO ai_config (key, value) VALUES ('current_model', ?)"
					).bind(newModel).run();

					return new Response(`Model changed to: ${newModel}`, { status: 200 });
				} else {
					return new Response("Invalid model name.", { status: 400 });
				}
			} catch (err) {
				return new Response("Bad Request", { status: 400 });
			}
		}

		return new Response("Not Found", { status: 404 });
	},
};