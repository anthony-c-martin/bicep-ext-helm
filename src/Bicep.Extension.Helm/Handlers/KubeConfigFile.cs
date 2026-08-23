using System.Text;

namespace Bicep.Extension.Helm.Handlers;

/// <summary>
/// Materializes the configured kubeconfig into a temporary file for the lifetime of a Helm
/// invocation. The Helm CLI only accepts a kubeconfig by path, so the content supplied through the
/// extension configuration has to be written to disk rather than passed through directly.
/// </summary>
internal sealed class KubeConfigFile : IDisposable
{
    private KubeConfigFile(string path)
    {
        Path = path;
    }

    public string Path { get; }

    /// <summary>
    /// Writes <paramref name="kubeConfig"/> to a private temporary file. The value may be either
    /// base64-encoded (as produced by Bicep's <c>base64()</c>) or raw kubeconfig YAML.
    /// </summary>
    public static KubeConfigFile Create(string kubeConfig)
    {
        var content = Decode(kubeConfig);
        var path = System.IO.Path.Join(
            System.IO.Path.GetTempPath(),
            $"bicep-ext-helm-{Guid.NewGuid():N}.kubeconfig");

        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };

        // Kubeconfigs carry cluster credentials, so restrict the file to the current user. On
        // Windows the file inherits the per-user temp directory's ACLs instead.
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        try
        {
            using var stream = new FileStream(path, options);
            stream.Write(content);
        }
        catch
        {
            TryDelete(path);
            throw;
        }

        return new KubeConfigFile(path);
    }

    public void Dispose() => TryDelete(Path);

    private static byte[] Decode(string kubeConfig)
    {
        var buffer = new byte[kubeConfig.Length / 4 * 3];

        return Convert.TryFromBase64String(kubeConfig, buffer, out var written)
            ? buffer.AsSpan(0, written).ToArray()
            : Encoding.UTF8.GetBytes(kubeConfig);
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort cleanup; the file lives under the temp directory.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
