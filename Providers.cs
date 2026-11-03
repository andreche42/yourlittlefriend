namespace YourLittleFriend;

// "porta la tua llm": va bene qualunque servizio che parla la lingua di openai, con la chiave dell'utente.
// niente elenco chiuso: questi sono solo i più comuni, e con "altro" si può mettere qualsiasi indirizzo.
public record Provider(string Id, string Name, string Base, bool NeedsKey, string KeysUrl);

public static class Providers
{
    public const string Local = "ollama";    // il modello che gira sul pc
    public const string Custom = "custom";   // indirizzo scritto a mano

    public static readonly Provider[] All =
    {
        new("openai", "OpenAI (ChatGPT)", "https://api.openai.com/v1", true, "https://platform.openai.com/api-keys"),
        new("anthropic", "Claude (Anthropic)", "https://api.anthropic.com/v1", true, "https://console.anthropic.com/settings/keys"),
        new("gemini", "Gemini (Google)", "https://generativelanguage.googleapis.com/v1beta/openai", true, "https://aistudio.google.com/apikey"),
        new("nvidia", "NVIDIA (NIM)", "https://integrate.api.nvidia.com/v1", true, "https://build.nvidia.com/settings/api-keys"),
        new("groq", "Groq", "https://api.groq.com/openai/v1", true, "https://console.groq.com/keys"),
        new("mistral", "Mistral", "https://api.mistral.ai/v1", true, "https://console.mistral.ai/api-keys"),
        new("deepseek", "DeepSeek", "https://api.deepseek.com/v1", true, "https://platform.deepseek.com/api_keys"),
        new("xai", "Grok (xAI)", "https://api.x.ai/v1", true, "https://console.x.ai"),
        new("openrouter", "OpenRouter (tanti modelli insieme)", "https://openrouter.ai/api/v1", true, "https://openrouter.ai/keys"),
        new("together", "Together", "https://api.together.xyz/v1", true, "https://api.together.ai/settings/api-keys"),
        new("cerebras", "Cerebras", "https://api.cerebras.ai/v1", true, "https://cloud.cerebras.ai"),
        new("fireworks", "Fireworks", "https://api.fireworks.ai/inference/v1", true, "https://fireworks.ai/account/api-keys"),
        new("lmstudio", "LM Studio (sul tuo PC)", "http://localhost:1234/v1", false, ""),
        new(Custom, "Altro (compatibile OpenAI)", "", true, ""),
    };

    // se l'id non esiste (impostazioni vecchie o scritte a mano) si torna a ollama
    public static Provider Get(string? id) =>
        All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];

    public static bool Known(string? id) => All.Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
}
