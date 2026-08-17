/*
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TownOfHost.Modules;

public class PresetSummary
{
    [JsonPropertyName("id")] public string Id { get; set; }
    [JsonPropertyName("name")] public string Name { get; set; }
    [JsonPropertyName("uploaderName")] public string UploaderName { get; set; }
    [JsonPropertyName("description")] public string Description { get; set; }
    [JsonPropertyName("version")] public string Version { get; set; }
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = new();
    [JsonPropertyName("downloadCount")] public int DownloadCount { get; set; }
    [JsonPropertyName("createdAt")] public string CreatedAt { get; set; }
}

public sealed class PresetDetail : PresetSummary
{
    [JsonPropertyName("data")] public string Data { get; set; }
}

public sealed class PresetListResult
{
    [JsonPropertyName("presets")] public List<PresetSummary> Presets { get; set; } = new();
    [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
    [JsonPropertyName("page")] public int Page { get; set; }
    [JsonPropertyName("pageSize")] public int PageSize { get; set; }
}

public sealed class PresetTagInfo
{
    [JsonPropertyName("tag")] public string Tag { get; set; }
    [JsonPropertyName("count")] public int Count { get; set; }
}

public enum PresetSortOrder { Recent, Popular }

public static class PresetShareClient
{
    static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10.0) };
    static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    // TODO: Main.csにこの2つの設定値を追加してください(MatchmakingRelayUrl/Secretと同じ要領)。
    // ワーカーのプリセットAPIが別ホストになる場合はこちらを向けてください。
    static string BaseUrl => Main.PresetShareApiUrl;
    static string Secret => Main.PresetShareApiSecret;

    static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BaseUrl) && !BaseUrl.Equals("none", StringComparison.OrdinalIgnoreCase);

    public static async Task<PresetListResult> FetchListAsync(
        string search = null, string version = null, IEnumerable<string> tags = null,
        PresetSortOrder sort = PresetSortOrder.Recent, int page = 1, int pageSize = 5)
    {
        if (!IsConfigured) return new PresetListResult();

        try
        {
            var query = new List<string>
            {
                $"sort={(sort == PresetSortOrder.Popular ? "popular" : "recent")}",
                $"page={page}",
                $"pageSize={pageSize}",
            };
            if (!string.IsNullOrWhiteSpace(search)) query.Add($"search={Uri.EscapeDataString(search)}");
            if (!string.IsNullOrWhiteSpace(version)) query.Add($"version={Uri.EscapeDataString(version)}");
            var tagList = tags?.Where(t => !string.IsNullOrWhiteSpace(t)).ToArray() ?? Array.Empty<string>();
            if (tagList.Length > 0) query.Add($"tags={Uri.EscapeDataString(string.Join(",", tagList))}");

            var url = $"{BaseUrl.TrimEnd('/')}/presets?{string.Join("&", query)}";
            using var res = await Client.GetAsync(url).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                Logger.Warn($"PresetShare list failed: {(int)res.StatusCode} / {body}", nameof(PresetShareClient));
                return new PresetListResult();
            }
            return JsonSerializer.Deserialize<PresetListResult>(body, JsonOptions) ?? new PresetListResult();
        }
        catch (Exception e)
        {
            Logger.Exception(e, nameof(PresetShareClient));
            return new PresetListResult();
        }
    }

    public static async Task<List<PresetTagInfo>> FetchTagsAsync()
    {
        if (!IsConfigured) return new();
        try
        {
            var url = $"{BaseUrl.TrimEnd('/')}/presets/tags";
            using var res = await Client.GetAsync(url).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!res.IsSuccessStatusCode) return new();

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("tags", out var tagsElem)) return new();
            return JsonSerializer.Deserialize<List<PresetTagInfo>>(tagsElem.GetRawText(), JsonOptions) ?? new();
        }
        catch (Exception e)
        {
            Logger.Exception(e, nameof(PresetShareClient));
            return new();
        }
    }

    public static async Task<PresetDetail> DownloadAsync(string id)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(id)) return null;
        try
        {
            var url = $"{BaseUrl.TrimEnd('/')}/presets/{Uri.EscapeDataString(id)}";
            using var res = await Client.GetAsync(url).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                Logger.Warn($"PresetShare download failed: {(int)res.StatusCode} / {body}", nameof(PresetShareClient));
                return null;
            }
            return JsonSerializer.Deserialize<PresetDetail>(body, JsonOptions);
        }
        catch (Exception e)
        {
            Logger.Exception(e, nameof(PresetShareClient));
            return null;
        }
    }

    public static async Task<(bool Success, string Error)> UploadAsync(
        string name, string uploaderName, string description, string version, List<string> tags, string data)
    {
        if (!IsConfigured) return (false, "PresetShareApiUrlが未設定です");

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                name,
                uploaderName,
                description,
                version,
                tags = tags ?? new List<string>(),
                data,
            });

            var url = $"{BaseUrl.TrimEnd('/')}/presets";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            if (!string.IsNullOrWhiteSpace(Secret) && !Secret.Equals("none", StringComparison.OrdinalIgnoreCase))
                req.Headers.TryAddWithoutValidation("X-Relay-Secret", Secret);
            req.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            using var res = await Client.SendAsync(req).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                string err;
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    err = doc.RootElement.TryGetProperty("error", out var e) ? e.GetString() : body;
                }
                catch { err = body; }
                Logger.Warn($"PresetShare upload failed: {(int)res.StatusCode} / {err}", nameof(PresetShareClient));
                return (false, err);
            }
            return (true, null);
        }
        catch (Exception e)
        {
            Logger.Exception(e, nameof(PresetShareClient));
            return (false, e.Message);
        }
    }
}
*/