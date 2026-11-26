// Fight or Flight : Hide N Seek - publish tool
// Scans your addons folder, zips every addon that changed since the last run,
// and writes manifest.txt. Upload everything in the "upload" folder to the GitHub release.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

static class Publish
{
    const string DefaultAddons = @"C:\Program Files (x86)\Steam\steamapps\common\GarrysMod\garrysmod\addons";

    static int Main(string[] args)
    {
        int code = 0;
        try { code = Run(args); }
        catch (Exception ex) { Console.WriteLine(); Console.WriteLine("ERROR: " + ex.Message); code = 1; }
        if (args.Length == 0 || args.All(a => a != "--nopause"))
        {
            Console.WriteLine();
            Console.Write("Press Enter to close...");
            try { Console.ReadLine(); } catch { }
        }
        return code;
    }

    static int Run(string[] args)
    {
        string here = AppDomain.CurrentDomain.BaseDirectory;
        string cfg = Path.Combine(here, "publish_config.txt");
        string lastFile = Path.Combine(here, "last_published.txt");
        string upload = Path.Combine(here, "upload");

        string addons = args.FirstOrDefault(a => !a.StartsWith("--"));
        if (addons == null && File.Exists(cfg)) addons = File.ReadAllText(cfg).Trim();
        if (string.IsNullOrEmpty(addons) && Directory.Exists(DefaultAddons)) addons = DefaultAddons;
        while (string.IsNullOrEmpty(addons) || !Directory.Exists(addons))
        {
            Console.WriteLine("Type or paste the full path to your garrysmod\\addons folder:");
            addons = (Console.ReadLine() ?? "").Trim().Trim('"');
            if (addons.Length == 0) return 1;
        }
        try { File.WriteAllText(cfg, addons); } catch { }
        Console.WriteLine("Addons folder: " + addons);
        Console.WriteLine();

        // previous run: name -> (hash, size)
        var last = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (File.Exists(lastFile))
            foreach (var l in File.ReadAllLines(lastFile).Skip(1))
            {
                var p = l.Split('|');
                if (p.Length == 3) last[p[0]] = p;
            }

        // fresh upload folder
        if (Directory.Exists(upload)) Directory.Delete(upload, true);
        Directory.CreateDirectory(upload);

        var dirs = Directory.GetDirectories(addons).Select(Path.GetFileName)
                   .Where(n => !n.StartsWith(".")).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        var lines = new List<string>();
        var changed = new List<string>();
        int same = 0, i = 0;

        foreach (var name in dirs)
        {
            i++;
            string dir = Path.Combine(addons, name);
            string hash = HashFolder(dir);
            string[] old;
            string zip = Path.Combine(upload, name + ".zip");
            if (last.TryGetValue(name, out old) && old[1] == hash)
            {
                lines.Add(name + "|" + hash + "|" + old[2]);
                same++;
                Console.WriteLine("[{0}/{1}] {2}  (unchanged)", i, dirs.Count, name);
                continue;
            }
            Console.WriteLine("[{0}/{1}] {2}  zipping...", i, dirs.Count, name);
            ZipFolder(dir, zip);
            long size = new FileInfo(zip).Length;
            if (size > 1900L * 1024 * 1024)
                Console.WriteLine("   WARNING: " + name + ".zip is over 1.9 GB; GitHub's limit is 2 GB per file.");
            lines.Add(name + "|" + hash + "|" + size);
            changed.Add(name + ".zip");
        }

        var manifest = new List<string> { "FOF1" };
        manifest.AddRange(lines);
        File.WriteAllLines(Path.Combine(upload, "manifest.txt"), manifest.ToArray(), new UTF8Encoding(false));
        File.WriteAllLines(lastFile, manifest.ToArray(), new UTF8Encoding(false));

        Console.WriteLine();
        Console.WriteLine("Done. {0} addons: {1} changed, {2} unchanged.", dirs.Count, changed.Count, same);
        Console.WriteLine();
        Console.WriteLine("Upload these to the GitHub release (replace the old files with the same names):");
        Console.WriteLine("  manifest.txt");
        foreach (var c in changed) Console.WriteLine("  " + c);
        Console.WriteLine();
        Console.WriteLine("Folder: " + upload);
        try { Process.Start(new ProcessStartInfo(upload) { UseShellExecute = true }); } catch { }
        return 0;
    }

    // Same content -> same hash, whatever the file dates are.
    static string HashFolder(string dir)
    {
        var files = Directory.GetFiles(dir, "*", SearchOption.AllDirectories)
            .Select(f => new { Full = f, Rel = Rel(dir, f) })
            .OrderBy(f => f.Rel, StringComparer.Ordinal).ToList();
        var sb = new StringBuilder();
        using (var sha = SHA256.Create())
        {
            foreach (var f in files)
            {
                using (var s = File.OpenRead(f.Full))
                    sb.Append(f.Rel).Append('\t').Append(Hex(sha.ComputeHash(s))).Append('\n');
            }
            return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString())));
        }
    }

    static void ZipFolder(string dir, string zipPath)
    {
        using (var fs = new FileStream(zipPath, FileMode.Create))
        using (var za = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            {
                var e = za.CreateEntry(Rel(dir, f), CompressionLevel.Optimal);
                using (var es = e.Open())
                using (var s = File.OpenRead(f))
                    s.CopyTo(es);
            }
        }
    }

    static string Rel(string root, string full)
    {
        return full.Substring(root.TrimEnd('\\', '/').Length + 1).Replace('\\', '/');
    }

    static string Hex(byte[] b)
    {
        var sb = new StringBuilder(b.Length * 2);
        foreach (var x in b) sb.Append(x.ToString("x2"));
        return sb.ToString();
    }
}
