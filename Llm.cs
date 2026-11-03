using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json.Nodes;

namespace YourLittleFriend;

// client per l'ia: o il modello che gira sul pc (ollama, api sua) o un servizio online con la chiave dell'utente.
// l'online parla tutti la stessa lingua (quella di openai), quindi va bene chiunque: openai, claude, gemini,
// nvidia, groq, openrouter... e anche lm studio sul proprio pc.
public static class Llm
{
    public const string DefaultModel = "qwen3.5:4b";
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(3) };

    public static bool OnApi => !string.Equals(Settings.Current.ApiProvider, Providers.Local, StringComparison.OrdinalIgnoreCase);
    public static Provider Api => Providers.Get(Settings.Current.ApiProvider);
    public static string Model => OnApi ? Settings.Current.ApiModel : Settings.Current.Model;

    public static string ApiBase
    {
        get
        {
            var b = string.IsNullOrWhiteSpace(Settings.Current.ApiBase) ? Api.Base : Settings.Current.ApiBase.Trim();
            return b.TrimEnd('/');
        }
    }

    // c'è tutto per fare una domanda? (indirizzo, modello e, se serve, la chiave)
    public static bool Ready => !OnApi
        || (!string.IsNullOrWhiteSpace(ApiBase) && !string.IsNullOrWhiteSpace(Settings.Current.ApiModel)
            && (!Api.NeedsKey || !string.IsNullOrWhiteSpace(Settings.Current.ApiKey)));

    // se passi onText la risposta arriva in streaming: onText viene chiamato a ogni pezzo di testo
    public static async Task<JsonNode> Chat(JsonArray messages, JsonNode? tools, Action<string>? onText = null)
        => OnApi ? await ApiChat(messages, tools, onText) : await OllamaChat(messages, tools, onText);

    // ---- ollama: il modello sul pc ----
    static async Task<JsonNode> OllamaChat(JsonArray messages, JsonNode? tools, Action<string>? onText)
    {
        var body = new JsonObject
        {
            ["model"] = Model,
            ["messages"] = ToOllama(messages),
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

    // l'immagine, per ollama, va in "images": qui il contenuto a blocchi viene riportato al suo formato
    static JsonArray ToOllama(JsonArray messages)
    {
        var outp = new JsonArray();
        foreach (var n in messages)
        {
            if (n is not JsonObject m) continue;
            var o = new JsonObject { ["role"] = m["role"]?.ToString() ?? "user" };
            if (m["content"] is JsonArray blocks)
            {
                var text = new StringBuilder();
                var imgs = new JsonArray();
                foreach (var b in blocks)
                {
                    if (b?["type"]?.ToString() == "text") text.AppendLine(b!["text"]?.ToString() ?? "");
                    else if (b?["type"]?.ToString() == "image_url") imgs.Add(DataPart(b?["image_url"]?["url"]?.ToString()));
                }
                o["content"] = text.ToString().Trim();
                if (imgs.Count > 0) o["images"] = imgs;
            }
            else o["content"] = m["content"]?.ToString() ?? "";
            if (m["tool_calls"] is JsonArray tc) o["tool_calls"] = tc.DeepClone();
            if (m["tool_name"] is JsonNode tn) o["tool_name"] = tn.DeepClone();
            outp.Add(o);
        }
        return outp;
    }

    static string DataPart(string? url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        int comma = url.IndexOf(',');
        return comma >= 0 ? url[(comma + 1)..] : url;
    }

    // ---- api online: la chiave dell'utente, qualunque servizio compatibile openai ----
    static async Task<JsonNode> ApiChat(JsonArray messages, JsonNode? tools, Action<string>? onText)
    {
        if (!Ready) throw new Exception(Loc.L("manca la chiave api o il modello: mettili in Impostazioni (⚙) → Intelligenza artificiale, oppure passa all'IA sul tuo PC",
                                             "the api key or the model is missing: set them in Settings (⚙) → Artificial intelligence, or switch to the AI on your PC"));
        var body = new JsonObject
        {
            ["model"] = Model,
            ["messages"] = ToApi(messages),
            ["temperature"] = 0.3,
            ["stream"] = onText != null
        };
        if (tools != null) body["tools"] = tools.DeepClone();
        var res = await Send(HttpMethod.Post, "/chat/completions", body);

        if (onText == null)
        {
            var j = JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
            var m = j["choices"]?[0]?["message"] ?? throw new Exception(Loc.L("la risposta non contiene nessun messaggio", "the answer contains no message"));
            var msg = new JsonObject { ["role"] = "assistant", ["content"] = m["content"]?.ToString() ?? "" };
            if (Calls(m["tool_calls"]) is JsonArray calls) msg["tool_calls"] = calls;
            return msg;
        }

        // streaming: righe "data: {...}", con i pezzi di testo e le chiamate agli strumenti spezzate in più righe
        var text = new StringBuilder();
        var pending = new SortedDictionary<int, (string Name, string Args)>();
        using var sr = new StreamReader(await res.Content.ReadAsStreamAsync());
        string? line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            if (!line.StartsWith("data:", StringComparison.Ordinal)) continue;
            var payload = line[5..].Trim();
            if (payload.Length == 0) continue;
            if (payload == "[DONE]") break;
            var j = JsonNode.Parse(payload)!;
            if (j["error"] != null) throw new Exception(ErrorText(j["error"]!));
            var delta = j["choices"]?[0]?["delta"];
            if (delta == null) continue;
            if (delta["content"]?.ToString() is { Length: > 0 } piece) { text.Append(piece); onText(piece); }
            if (delta["tool_calls"] is JsonArray tcs)
                foreach (var tc in tcs)
                {
                    int i = int.TryParse(tc?["index"]?.ToString(), out var k) ? k : 0;
                    var fn = tc?["function"];
                    pending.TryGetValue(i, out var cur);
                    string name = fn?["name"]?.ToString() is { Length: > 0 } nm ? nm : cur.Name ?? "";
                    pending[i] = (name, (cur.Args ?? "") + (fn?["arguments"]?.ToString() ?? ""));
                }
        }
        var final = new JsonObject { ["role"] = "assistant", ["content"] = text.ToString() };
        if (pending.Count > 0)
        {
            var arr = new JsonArray();
            foreach (var (name, args) in pending.Values) arr.Add(Call(name, args));
            final["tool_calls"] = arr;
        }
        return final;
    }

    // richiesta all'api scelta. la chiave va nell'intestazione, come fanno tutti
    static async Task<HttpResponseMessage> Send(HttpMethod method, string path, JsonNode? body)
    {
        var url = ApiBase + path;
        var req = new HttpRequestMessage(method, url);
        var key = (Settings.Current.ApiKey ?? "").Trim();
        if (key.Length > 0) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
        if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        HttpResponseMessage res;
        try { res = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead); }
        catch (TaskCanceledException) { throw new Exception(Loc.L("l'IA non ha risposto in tempo", "the AI didn't answer in time")); }
        catch (HttpRequestException ex)
        {
            throw new Exception(Loc.L($"{Api.Name} non risponde ({ApiBase}): controlla la connessione e l'indirizzo. {ex.Message}",
                                      $"{Api.Name} isn't responding ({ApiBase}): check your connection and the address. {ex.Message}"));
        }
        if (!res.IsSuccessStatusCode)
        {
            var raw = await res.Content.ReadAsStringAsync();
            throw new Exception(Status(res, raw));
        }
        return res;
    }

    // messaggio d'errore comprensibile: gli id degli errori non dicono niente a chi usa l'app
    static string Status(HttpResponseMessage res, string raw)
    {
        string? detail = null;
        try { var j = JsonNode.Parse(raw); detail = j?["error"]?["message"]?.ToString() ?? j?["message"]?.ToString(); } catch { }
        if (string.IsNullOrWhiteSpace(detail)) detail = raw.Length > 300 ? raw[..300] : raw;
        string head = (int)res.StatusCode switch
        {
            400 => Loc.L("la richiesta non va bene (spesso è il modello, o uno strumento che questo servizio non accetta)", "the request isn't accepted (often the model, or a tool this service doesn't allow)"),
            401 or 403 => Loc.L("la chiave non è valida, o non può usare questo modello", "the key isn't valid, or it can't use this model"),
            402 => Loc.L("credito finito su questo servizio", "no credit left on this service"),
            404 => Loc.L("questo indirizzo o questo modello non esiste", "this address or model doesn't exist"),
            429 => Loc.L("troppe richieste (o limite gratuito finito): aspetta un attimo", "too many requests (or the free limit is used up): wait a moment"),
            >= 500 => Loc.L("il servizio dell'IA è in difficoltà, riprova tra poco", "the AI service is having trouble, try again in a moment"),
            _ => $"HTTP {(int)res.StatusCode}"
        };
        // capita con un'immagine allegata a un modello che non le legge: l'errore del servizio non lo dice
        if ((int)res.StatusCode == 400 && raw.Contains("must be a string", StringComparison.OrdinalIgnoreCase))
            head = Loc.L("questo modello non legge le immagini: scegline uno che le legge (un'immagine non si può mandare come testo)",
                         "this model doesn't read images: pick one that does (an image can't be sent as text)");
        return string.IsNullOrWhiteSpace(detail) ? head : $"{head}\n{detail}";
    }

    static string ErrorText(JsonNode err) => err["message"]?.ToString() ?? err.ToString();

    // i messaggi come li vuole l'api: il risultato di uno strumento deve portare l'id della chiamata
    static JsonArray ToApi(JsonArray messages)
    {
        var outp = new JsonArray();
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);   // ultimo id usato, per nome dello strumento
        foreach (var n in messages)
        {
            if (n is not JsonObject m) continue;
            var role = m["role"]?.ToString() ?? "user";
            var o = new JsonObject { ["role"] = role };
            if (role == "tool")
            {
                o["content"] = m["content"]?.ToString() ?? "";
                var name = m["tool_name"]?.ToString() ?? "";
                o["tool_call_id"] = m["tool_call_id"]?.ToString() ?? (ids.TryGetValue(name, out var id) ? id : name);
                outp.Add(o);
                continue;
            }
            // il contenuto a blocchi si tiene solo se c'è davvero un'immagine da allegare: per un file di testo
            // i servizi online vogliono una stringa, e con un array rispondono "messages[0].content must be a string"
            o["content"] = m["content"] is JsonArray blocks && HasImage(blocks) ? blocks.DeepClone() : Flatten(m["content"]);
            if (m["tool_calls"] is JsonArray tcs && tcs.Count > 0)
            {
                var arr = new JsonArray();
                foreach (var t in tcs)
                {
                    var name = t?["function"]?["name"]?.ToString() ?? "";
                    var id = t?["id"]?.ToString();
                    if (string.IsNullOrWhiteSpace(id)) id = "call_" + Guid.NewGuid().ToString("N")[..8];
                    ids[name] = id;
                    var args = t?["function"]?["arguments"];
                    arr.Add(new JsonObject
                    {
                        ["id"] = id,
                        ["type"] = "function",
                        ["function"] = new JsonObject
                        {
                            ["name"] = name,
                            ["arguments"] = args is JsonValue ? args.ToString() : args?.ToJsonString() ?? "{}"
                        }
                    });
                }
                o["tool_calls"] = arr;
            }
            outp.Add(o);
        }
        return outp;
    }

    // il contenuto a blocchi contiene un'immagine? (solo allora ha senso mandarlo come array)
    static bool HasImage(JsonArray blocks)
    {
        foreach (var b in blocks) if (b?["type"]?.ToString() == "image_url") return true;
        return false;
    }

    // blocchi di solo testo (un file allegato, per esempio) tornano una stringa unica, come la vogliono tutti
    static string Flatten(JsonNode? content)
    {
        if (content is not JsonArray blocks) return content?.ToString() ?? "";
        var parts = new List<string>();
        foreach (var b in blocks)
        {
            if (b?["type"]?.ToString() == "text") parts.Add(b["text"]?.ToString() ?? "");
            else if (b is JsonValue v) parts.Add(v.ToString());
        }
        return string.Join("\n\n", parts).Trim();
    }

    // una chiamata allo strumento nella forma interna (come la manda ollama): nome e argomenti già oggetto
    static JsonObject Call(string name, string args)
    {
        JsonNode? a;
        try { a = JsonNode.Parse(string.IsNullOrWhiteSpace(args) ? "{}" : args); } catch { a = null; }
        return new JsonObject
        {
            ["function"] = new JsonObject { ["name"] = name, ["arguments"] = a ?? new JsonObject() }
        };
    }

    static JsonArray? Calls(JsonNode? calls)
    {
        if (calls is not JsonArray arr || arr.Count == 0) return null;
        var outp = new JsonArray();
        foreach (var c in arr)
            outp.Add(Call(c?["function"]?["name"]?.ToString() ?? "", c?["function"]?["arguments"]?.ToString() ?? "{}"));
        return outp;
    }

    // i modelli disponibili con questa chiave: così non vanno scritti a mano
    public static async Task<List<string>> ApiModels()
    {
        var res = await Send(HttpMethod.Get, "/models", null);
        var j = JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
        var list = new List<string>();
        if (j["data"] is JsonArray arr) foreach (var m in arr) Add(list, m?["id"]?.ToString());
        else if (j["models"] is JsonArray arr2) foreach (var m in arr2) Add(list, m?["id"]?.ToString() ?? m?["name"]?.ToString());
        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;

        static void Add(List<string> l, string? id) { if (!string.IsNullOrWhiteSpace(id) && !l.Contains(id)) l.Add(id); }
    }

    // una domanda piccolissima: serve solo a capire se chiave e indirizzo funzionano
    public static async Task<string> Test()
    {
        var msgs = new JsonArray { new JsonObject { ["role"] = "user", ["content"] = "ciao, rispondi con una parola" } };
        var msg = await Chat(msgs, null, null);
        var t = (msg["content"]?.ToString() ?? "").Trim();
        return t.Length > 120 ? t[..120] : t;
    }
}
