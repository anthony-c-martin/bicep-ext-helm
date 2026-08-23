using Bicep.Local.Extension.Host.Handlers;

namespace Bicep.Extension.Helm.Handlers;

public class ReleaseHandler(IHelmCommandRunner helm) : TypedResourceHandler<Release, ReleaseIdentifiers, Configuration>
{
    protected override async Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Config.KubeConfig))
        {
            throw new ResourceErrorException("InvalidConfiguration", "The 'kubeConfig' extension configuration value is required and cannot be empty.");
        }

        await helm.RunAsync(["version", "--short"], cancellationToken);

        using var kubeConfig = KubeConfigFile.Create(request.Config.KubeConfig);

        await helm.RunAsync(BuildUpgradeArgs(request.Properties, kubeConfig.Path), cancellationToken);

        return GetResponse(request);
    }

    protected override ReleaseIdentifiers GetIdentifiers(Release properties)
        => new()
        {
            Name = properties.Name,
        };

    internal static IEnumerable<string> BuildUpgradeArgs(Release release, string kubeConfigPath)
    {
        var args = new List<string>
        {
            "upgrade",
            "--install",
            release.Name,
            release.Chart,
            "--repo",
            release.Repository,
            "--kubeconfig",
            kubeConfigPath,
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
}
