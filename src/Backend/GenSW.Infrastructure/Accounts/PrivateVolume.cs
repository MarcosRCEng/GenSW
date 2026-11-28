using GenSW.Application.Accounts;

namespace GenSW.Infrastructure.Accounts;

public static class PrivateVolume
{
    public const string MarkerName = ".gensw-volume-id";

    public static string CanonicalPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new AccountUnavailableException();
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    public static bool Overlaps(string a, string b)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return a.Equals(b, comparison) || Inside(a, b, comparison) || Inside(b, a, comparison);
    }

    private static bool Inside(string parent, string path, StringComparison comparison) =>
        path.StartsWith(Path.EndsInDirectorySeparator(parent) ? parent : parent + Path.DirectorySeparatorChar, comparison);

    public static void Verify(string root, Guid expected)
    {
        using var lease = Hold(root, expected, FileShare.Read);
    }

    public static FileStream Hold(string root, Guid expected, FileShare share)
    {
        for (var directory = new DirectoryInfo(root); directory is not null; directory = directory.Parent)
        {
            if (!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) != 0 ||
                directory.Name.Equals("wwwroot", StringComparison.OrdinalIgnoreCase) ||
                directory.Name.Equals(".gensw", StringComparison.OrdinalIgnoreCase) ||
                File.Exists(Path.Combine(directory.FullName, ".git")) || Directory.Exists(Path.Combine(directory.FullName, ".git")))
                throw new AccountUnavailableException();
        }

        var marker = Path.Combine(root, MarkerName);
        if ((File.GetAttributes(marker) & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            throw new AccountUnavailableException();

        var file = new FileStream(marker, FileMode.Open, FileAccess.Read, share);
        try
        {
            if (file.Length is < 36 or > 40)
                throw new AccountUnavailableException();
            using var reader = new StreamReader(file, leaveOpen: true);
            if (!Guid.TryParseExact(reader.ReadToEnd().Trim(), "D", out var actual) || actual != expected)
                throw new AccountUnavailableException();
            return file;
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }
}
