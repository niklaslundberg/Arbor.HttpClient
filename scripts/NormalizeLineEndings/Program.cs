// Normalizes line endings of text files to the OS native newline (Environment.NewLine).
// Excludes the .git directory and any binary files (detected by presence of a null byte).
// Supports a "--dry-run" flag which reports files that would be changed without modifying them.
// Usage: dotnet run scripts/NormalizeLineEndings/Program.cs [--dry-run]

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics;
// Define known text extensions.
string[] TextFileExtensions = new[]
{
    ".cs", ".csproj", ".txt", ".md", ".json", ".xml", ".yml", ".yaml", ".axaml", ".props", ".targets", ".gitignore", ".config"
};

bool dryRun = args.Contains("--dry-run");
string root = Directory.GetCurrentDirectory();
var files = GetFiles(root);
int changed = 0;
foreach (var file in files)
{
    // Skip files ignored by .gitignore
    if (IsGitIgnored(file))
        continue;

    if (IsBinary(file))
        continue;

    string original = File.ReadAllText(file);
    string normalized = NormalizeNewlines(original);
    if (original != normalized)
    {
        changed++;
        if (dryRun)
        {
            Console.WriteLine($"[Dry Run] Would normalize: {file}");
        }
        else
        {
            File.WriteAllText(file, normalized);
            Console.WriteLine($"Normalized: {file}");
        }
    }
}

Console.WriteLine($"{(dryRun ? "Dry run" : "Completed")} – {changed} file(s) {(dryRun ? "would be" : "were")} modified.");

// Recursively get all files, excluding any path that contains a ".git" folder.
IEnumerable<string> GetFiles(string rootPath)
{
    var stack = new Stack<string>();
    stack.Push(rootPath);
    while (stack.Count > 0)
    {
        var current = stack.Pop();
        foreach (var dir in Directory.GetDirectories(current))
        {
            var dirName = Path.GetFileName(dir);
            if (string.Equals(dirName, ".git", StringComparison.OrdinalIgnoreCase))
                continue;
            stack.Push(dir);
        }

        foreach (var file in Directory.GetFiles(current))
        {
            var ext = Path.GetExtension(file);
            if (TextFileExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(ext))
                yield return file;
        }
    }
}

bool IsBinary(string path)
{
    const int sampleSize = 8000;
    byte[] buffer = new byte[sampleSize];
    try
    {
        using var stream = File.OpenRead(path);
        int bytesRead = stream.Read(buffer, 0, sampleSize);
        for (int i = 0; i < bytesRead; i++)
        {
            if (buffer[i] == 0)
                return true;
        }
        return false;
    }
    catch
    {
        return true;
    }
}

// Determines if a file is ignored by .gitignore using "git check-ignore".
static bool IsGitIgnored(string filePath)
{
    // Locate the repository root containing the .git folder.
    string dir = Path.GetDirectoryName(filePath) ?? "";
    while (!string.IsNullOrEmpty(dir) && !Directory.Exists(Path.Combine(dir, ".git")))
    {
        dir = Path.GetDirectoryName(dir);
    }
    if (string.IsNullOrEmpty(dir))
        return false; // No .git folder found – treat as not ignored.

    var startInfo = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = $"check-ignore -q \"{filePath}\"",
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = dir
    };
    try
    {
        using var process = Process.Start(startInfo);
        process.WaitForExit();
        return process.ExitCode == 0; // 0 = ignored, 1 = not ignored
    }
    catch
    {
        // If git is unavailable, assume file is not ignored.
        return false;
    }
}

string NormalizeNewlines(string text)
{
    string unified = text.Replace("\r\n", "\n").Replace("\r", "\n");
    return unified.Replace("\n", Environment.NewLine);
}
