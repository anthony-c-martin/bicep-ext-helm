using System.ComponentModel;
using System.Diagnostics;
using Bicep.Local.Extension.Host.Handlers;

namespace Bicep.Extension.Helm.Handlers;

public class ReleaseHandler : TypedResourceHandler<Release, ReleaseIdentifiers, Configuration>
{
    protected override async Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
    {
        await EnsureHelmInstalled(cancellationToken);

        var release = request.Properties;
        var args = BuildUpgradeArgs(release);

        await RunHelmCommand(args, cancellationToken);

        return GetResponse(request);
    }

    protected override ReleaseIdentifiers GetIdentifiers(Release properties)
        => new()
        {
            Name = properties.Name,
        };

    private static IEnumerable<string> BuildUpgradeArgs(Release release)
    {
        var args = new List<string>
        {
            "upgrade",
            "--install",
            release.Name,
            release.Chart,
            "--repo",
            release.Repository,
        };

        if (!string.IsNullOrWhiteSpace(release.Namespace))
        {
            args.Add("--namespace");
            args.Add(release.Namespace);
            args.Add("--create-namespace");
        }

        if (!string.IsNullOrWhiteSpace(release.Version))
        {
            args.Add("--version");
            args.Add(release.Version);
        }

        if (!string.IsNullOrWhiteSpace(release.ValuesFile))
        {
            args.Add("--values");
            args.Add(release.ValuesFile);
        }

        if (release.Set is not null)
        {
            foreach (var setValue in release.Set)
            {
                args.Add("--set");
                args.Add($"{setValue.Name}={setValue.Value}");
            }
        }

        return args;
    }

    private static async Task EnsureHelmInstalled(CancellationToken cancellationToken)
    {
        try
        {
            await RunHelmCommand(["version", "--short"], cancellationToken);
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("Unable to start 'helm'. Ensure Helm CLI is installed and available on PATH.", ex);
        }
    }

    private static async Task RunHelmCommand(IEnumerable<string> args, CancellationToken cancellationToken)
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
