using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace YourLittleFriend;

// cerca e riproduce su spotify. senza SPOTIFY_CLIENT_ID apre solo la ricerca nell'app.
public static class Spotify
{
    static readonly HttpClient H = new();
    static string? access;
    static DateTime exp;
    const string Redirect = "http://127.0.0.1:8888/";
    static string Id => Environment.GetEnvironmentVariable("SPOTIFY_CLIENT_ID") ?? "";
    static string TokenFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "yourlittlefriend", "spotify.txt");

    public static async Task<string> Play(string q)
    {
        if (Id == "")
        {
            Actions.Open("spotify:search:" + Uri.EscapeDataString(q));
            return "ho aperto la ricerca su spotify, premi play sul primo risultato (per farlo partire da solo serve SPOTIFY_CLIENT_ID)";
        }
        var (_, body) = await Send(HttpMethod.Get, $"https://api.spotify.com/v1/search?q={Uri.EscapeDataString(q)}&type=track&limit=1");
        var item = JsonNode.Parse(body)?["tracks"]?["items"]?[0];
        if (item == null) return "nessun risultato su spotify";
        var uri = item["uri"]!.ToString();
        var label = $"{item["name"]} - {item["artists"]?[0]?["name"]}";
        int code = await PlayUri(uri);
        if (code == 404) { Actions.Open("spotify:"); await Task.Delay(5000); code = await PlayUri(uri); }
        return code is 200 or 204 ? $"in riproduzione: {label}" : $"spotify ha risposto {code} (serve account premium e spotify aperto)";
    }

    static async Task<int> PlayUri(string uri)
    {
        var (s, _) = await Send(HttpMethod.Put, "https://api.spotify.com/v1/me/player/play",
            new JsonObject { ["uris"] = new JsonArray(JsonValue.Create(uri)) }.ToJsonString());
        return s;
    }

    static async Task<(int, string)> Send(HttpMethod m, string url, string? body = null)
    {
        var req = new HttpRequestMessage(m, url);
        req.Headers.Authorization = new("Bearer", await Token());
        if (body != null) req.Content = new StringContent(body, Encoding.UTF8, "application/json");
        var r = await H.SendAsync(req);
        return ((int)r.StatusCode, await r.Content.ReadAsStringAsync());
    }

    static async Task<string> Token()
    {
        if (access != null && DateTime.UtcNow < exp) return access;
        if (File.Exists(TokenFile) &&
            await Grant(new() { ["grant_type"] = "refresh_token", ["refresh_token"] = File.ReadAllText(TokenFile), ["client_id"] = Id }))
            return access!;
        await Login();
        return access!;
    }

    static async Task<bool> Grant(Dictionary<string, string> f)
    {
        var res = await H.PostAsync("https://accounts.spotify.com/api/token", new FormUrlEncodedContent(f));
        if (!res.IsSuccessStatusCode) return false;
        var j = JsonNode.Parse(await res.Content.ReadAsStringAsync())!;
        access = j["access_token"]!.ToString();
        exp = DateTime.UtcNow.AddSeconds((int)j["expires_in"]! - 60);
        if (j["refresh_token"] != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(TokenFile)!);
            File.WriteAllText(TokenFile, j["refresh_token"]!.ToString());
        }
        return true;
    }

    static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    // login una tantum nel browser (pkce), poi si usa il refresh token salvato
    static async Task Login()
    {
        var ver = B64(RandomNumberGenerator.GetBytes(48));
        var chal = B64(SHA256.HashData(Encoding.ASCII.GetBytes(ver)));
        var scope = Uri.EscapeDataString("user-modify-playback-state user-read-playback-state");
        using var l = new HttpListener();
        l.Prefixes.Add(Redirect);
        l.Start();
        Actions.Open($"https://accounts.spotify.com/authorize?client_id={Id}&response_type=code&redirect_uri={Uri.EscapeDataString(Redirect)}&scope={scope}&code_challenge_method=S256&code_challenge={chal}");
        var ctx = await l.GetContextAsync().WaitAsync(TimeSpan.FromMinutes(2));
        var code = ctx.Request.QueryString["code"];
        ctx.Response.OutputStream.Write(Encoding.UTF8.GetBytes("fatto, puoi chiudere questa pagina"));
        ctx.Response.Close();
        if (code == null || !await Grant(new() { ["grant_type"] = "authorization_code", ["code"] = code, ["redirect_uri"] = Redirect, ["client_id"] = Id, ["code_verifier"] = ver }))
            throw new Exception("login spotify fallito");
    }
}
