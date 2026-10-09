using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace YourLittleFriend;

// client minimale per ollama (llm locale)
public static class Llm
{
    public const string DefaultModel = "qwen3.5:4b";
    public static string Model => Settings.Current.Model;
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    // se passi onText la risposta arriva in streaming: onText viene chiamato a ogni pezzo di testo
    public static async Task<JsonNode> Chat(JsonArray messages, JsonNode? tools, Action<string>? onText = null)
    {
        var body = new JsonObject
        {
            ["model"] = Model,
            ["messages"] = messages.DeepClone(),
            ["stream"] = onText != null,
            ["think"] = false,
            ["options"] = new JsonObject { ["temperature"] = 0.3 }
        };
        if (tools != null) body["tools"] = tools.DeepClone();
        HttpResponseMessage res;
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/chat")
            { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
            res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        }
        catch (HttpRequestException) { throw new Exception(Loc.L($"ollama non risponde: apri Impostazioni (⚙) per installarlo, oppure scrivi nel terminale: ollama pull {Model}", $"ollama is not responding: open Settings (⚙) to install it, or run in a terminal: ollama pull {Model}")); }
        if (!res.IsSuccessStatusCode) throw new Exception(await res.Content.ReadAsStringAsync());
        if (onText == null) return JsonNode.Parse(await res.Content.ReadAsStringAsync())!["message"]!;

        // streaming: ollama manda una riga json per pezzo di testo
        var text = new StringBuilder();
        var calls = new JsonArray();
        using var sr = new StreamReader(await res.Content.ReadAsStreamAsync());
        string? line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            if (line.Length == 0) continue;
            var j = JsonNode.Parse(line)!;
            if (j["error"] != null) throw new Exception(j["error"]!.ToString());
            var m = j["message"];
            if (m == null) continue;
            var piece = m["content"]?.ToString();
            if (!string.IsNullOrEmpty(piece)) { text.Append(piece); onText(piece); }
            if (m["tool_calls"] is JsonArray tc) foreach (var t in tc) calls.Add(t!.DeepClone());
        }
        var msg = new JsonObject { ["role"] = "assistant", ["content"] = text.ToString() };
        if (calls.Count > 0) msg["tool_calls"] = calls;
        return msg;
    }
}
