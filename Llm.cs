using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace YourLittleFriend;

// client minimale per ollama (llm locale)
public static class Llm
{
    public const string Model = "qwen3.5:4b";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    public static async Task<JsonNode> Chat(JsonArray messages, JsonNode? tools)
    {
        var body = new JsonObject
        {
            ["model"] = Model,
            ["messages"] = messages.DeepClone(),
            ["stream"] = false,
            ["think"] = false,
            ["options"] = new JsonObject { ["temperature"] = 0.3 }
        };
        if (tools != null) body["tools"] = tools.DeepClone();
        HttpResponseMessage res;
        try { res = await Http.PostAsync("http://localhost:11434/api/chat", new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")); }
        catch (HttpRequestException) { throw new Exception($"ollama non risponde: installalo da ollama.com e poi scrivi nel terminale: ollama pull {Model}"); }
        var txt = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception(txt);
        return JsonNode.Parse(txt)!["message"]!;
    }

    public static string Clean(string? s) => Regex.Replace(s ?? "", "<think>.*?</think>", "", RegexOptions.Singleline).Trim();
}
