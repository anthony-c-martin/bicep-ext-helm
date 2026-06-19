using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace Bicep.Extension.Helm;

public class Configuration
{
    [TypeProperty("The resource ID of the AKS cluster.", ObjectTypePropertyFlags.Required)]
    public required string AksResourceId { get; set; }
}

public class ReleaseIdentifiers
{
    [TypeProperty("The Helm release name.", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string Name { get; set; }
}

public class HelmSetValue
{
    [TypeProperty("The Helm values path to set (for example, service.type).", ObjectTypePropertyFlags.Required)]
    public required string Name { get; set; }

    [TypeProperty("The value to assign to the specified Helm values path.", ObjectTypePropertyFlags.Required)]
    public required string Value { get; set; }
}

[ResourceType("Release")]
public class Release : ReleaseIdentifiers
{
    [TypeProperty("The Helm chart repository URL.", ObjectTypePropertyFlags.Required)]
    public required string Repository { get; set; }

    [TypeProperty("The Helm chart name.", ObjectTypePropertyFlags.Required)]
    public required string Chart { get; set; }

    [TypeProperty("The Kubernetes namespace to deploy into.")]
    public string? Namespace { get; set; }

    [TypeProperty("Optional Helm chart version.")]
    public string? Version { get; set; }

    [TypeProperty("Optional values file path to pass with --values.")]
    public string? ValuesFile { get; set; }

    [TypeProperty("Optional list of --set overrides.")]
    public List<HelmSetValue>? Set { get; set; }
}