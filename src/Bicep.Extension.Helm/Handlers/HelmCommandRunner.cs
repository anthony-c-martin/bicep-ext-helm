using System.ComponentModel;
using System.Diagnostics;

namespace Bicep.Extension.Helm.Handlers;

/// <summary>
/// Invokes the Helm CLI. Abstracted so that handlers can be tested without a Helm installation.
/// </summary>
public interface IHelmCommandRunner
{
    Task RunAsync(IEnumerable<string> args, CancellationToken cancellationToken);
}

public sealed class HelmCommandRunner : IHelmCommandRunner
{
    public async Task RunAsync(IEnumerable<string> args, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo =
            {
                FileName = "helm",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            },
        };

        foreach (var arg in args)
        {
            process.StartInfo.ArgumentList.Add(arg);
        }

        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("Unable to start 'helm'. Ensure Helm CLI is installed and available on PATH.", ex);
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var stdOut = await stdOutTask;
        var stdErr = await stdErrTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Helm command failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                $"stdout: {stdOut}{Environment.NewLine}" +
                $"stderr: {stdErr}");
        }
    }
}
