using System.Text;
using System.Text.Json;
using Bicep.Extension.Helm.Handlers;
using Bicep.Local.Extension.Host.Handlers;
using Bicep.Local.Rpc;

namespace Bicep.Extension.Helm.Tests;

/// <summary>
/// Helpers for invoking resource handlers through their public <see cref="IResourceHandler"/> entry
/// point, mirroring how the Bicep local-deploy host calls them (JSON in, JSON out).
/// </summary>
public static class HandlerHarness
{
    // The Bicep host exchanges properties/config as camelCase JSON.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// A minimal kubeconfig, and the base64 encoding of it that samples pass to the extension.
    /// </summary>
    public const string TestKubeConfigContent = "apiVersion: v1\nkind: Config\nclusters: []\n";

    public static readonly string TestKubeConfig = Convert.ToBase64String(Encoding.UTF8.GetBytes(TestKubeConfigContent));

    public static Task<LocalExtensibilityOperationResponse> CreateOrUpdateAsync(
        IResourceHandler handler,
        string type,
        object properties,
        string? kubeConfig = null,
        CancellationToken cancellationToken = default)
    {
        var spec = new ResourceSpecification
        {
            Type = type,
            Config = JsonSerializer.Serialize(new { kubeConfig = kubeConfig ?? TestKubeConfig }, SerializerOptions),
            Properties = JsonSerializer.Serialize(properties, SerializerOptions),
        };

        return handler.CreateOrUpdate(spec, cancellationToken);
    }

    public static JsonElement ResourceProperties(this LocalExtensibilityOperationResponse response)
    {
        Assert.IsNull(response.ErrorData);
        Assert.IsNotNull(response.Resource);
        return JsonSerializer.Deserialize<JsonElement>(response.Resource.Properties);
    }

    public static JsonElement ResourceIdentifiers(this LocalExtensibilityOperationResponse response)
    {
        Assert.IsNull(response.ErrorData);
        Assert.IsNotNull(response.Resource);
        return JsonSerializer.Deserialize<JsonElement>(response.Resource.Identifiers);
    }

    public static Error ExpectError(this LocalExtensibilityOperationResponse response)
    {
        Assert.IsNotNull(response.ErrorData);
        Assert.IsNotNull(response.ErrorData.Error);
        return response.ErrorData.Error;
    }
}

/// <summary>
/// Records the Helm invocations a handler makes, and optionally fails them, so that handler
/// behavior can be asserted without a Helm installation.
/// </summary>
public sealed class FakeHelmCommandRunner : IHelmCommandRunner
{
    private readonly Func<IReadOnlyList<string>, Exception?>? failureSelector;

    public FakeHelmCommandRunner(Func<IReadOnlyList<string>, Exception?>? failureSelector = null)
    {
        this.failureSelector = failureSelector;
    }

    public List<IReadOnlyList<string>> Invocations { get; } = [];

    /// <summary>
    /// The kubeconfig file paths observed during each invocation that passed <c>--kubeconfig</c>.
    /// </summary>
    public List<string> KubeConfigPaths { get; } = [];

    /// <summary>
    /// The kubeconfig file contents, captured while the file still exists (the handler deletes it
    /// once the Helm invocation completes).
    /// </summary>
    public List<string> KubeConfigContents { get; } = [];

    /// <summary>
    /// The arguments of the <c>helm upgrade</c> invocation, which follows the version preflight.
    /// </summary>
    public IReadOnlyList<string> UpgradeArgs
    {
        get
        {
            var upgrade = Invocations.SingleOrDefault(args => args is ["upgrade", ..]);
            Assert.IsNotNull(upgrade, "Expected the handler to run 'helm upgrade'.");
            return upgrade;
        }
    }

    public Task RunAsync(IEnumerable<string> args, CancellationToken cancellationToken)
    {
        var recorded = args.ToArray();
        Invocations.Add(recorded);

        var kubeConfigIndex = Array.IndexOf(recorded, "--kubeconfig");
        if (kubeConfigIndex >= 0 && kubeConfigIndex + 1 < recorded.Length)
        {
            var path = recorded[kubeConfigIndex + 1];
            KubeConfigPaths.Add(path);
            KubeConfigContents.Add(File.ReadAllText(path));
        }

        return failureSelector?.Invoke(recorded) is { } exception
            ? Task.FromException(exception)
            : Task.CompletedTask;
    }
}
