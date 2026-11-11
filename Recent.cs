using System.IO;

namespace YourLittleFriend;

// "apri i download", "apri la bolletta": un percorso diretto, una cartella che si può nominare, oppure un file recente cercato per nome
public static class Recent
{
    static readonly (string Dir, string[] Words)[] Folders =
    {
        ("Downloads", new[] { "download", "scarica" }),
        ("Documents", new[] { "documenti", "documents" }),
        ("Pictures", new[] { "immagini", "foto", "pictures" }),
        ("Music", new[] { "musica", "music" }),
        ("Videos", new[] { "video" }),
        ("Desktop", new[] { "desktop", "scrivania" }),
    };

    public static string? Resolve(string name)
    {
        var n = name.Trim().Trim('"');
        if (n.Length == 0) return null;

        // 1. è già un percorso (anche con %VARIABILI%)
        try
        {
            var p = Environment.ExpandEnvironmentVariables(n);
            if (File.Exists(p) || Directory.Exists(p)) return p;
        }
        catch { }

        // 2. una cartella che si può nominare: "download", "documenti/bolletta.pdf"
        var parts = n.Split('/', '\\');
        foreach (var (dir, words) in Folders)
        {
            if (!words.Any(w => parts[0].Contains(w, StringComparison.OrdinalIgnoreCase))) continue;
            var root = Environment.GetFolderPath(dir switch
            {
                "Downloads" => Environment.SpecialFolder.UserProfile,
                "Documents" => Environment.SpecialFolder.MyDocuments,
                "Pictures" => Environment.SpecialFolder.MyPictures,
                "Music" => Environment.SpecialFolder.MyMusic,
                "Videos" => Environment.SpecialFolder.MyVideos,
                _ => Environment.SpecialFolder.DesktopDirectory
            });
            if (dir == "Downloads") root = Path.Combine(root, "Downloads");
            if (!Directory.Exists(root)) continue;
            if (parts.Length == 1) return root;
            var target = Path.Combine(new[] { root }.Concat(parts.Skip(1)).ToArray());
            if (File.Exists(target) || Directory.Exists(target)) return target;
        }

        // 3. fra i file recenti di windows, quello che contiene il nome (il più nuovo)
        try
        {
            var recent = Environment.GetFolderPath(Environment.SpecialFolder.Recent);
            if (Directory.Exists(recent))
            {
                var hit = Directory.EnumerateFiles(recent, "*.lnk")
                    .Where(f => Path.GetFileNameWithoutExtension(f).Contains(n, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();
                if (hit != null) return hit;
            }
        }
        catch { }

        // 4. sul desktop, per nome
        try
        {
            var desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var hit = Directory.EnumerateFileSystemEntries(desk)
                .Where(f => Path.GetFileName(f).Contains(n, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(File.GetLastWriteTime)
                .FirstOrDefault();
            if (hit != null) return hit;
        }
        catch { }
        return null;
    }
}
