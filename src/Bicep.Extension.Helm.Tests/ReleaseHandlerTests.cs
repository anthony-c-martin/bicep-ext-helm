using System.Text;
using System.Text.Json;
using Bicep.Extension.Helm.Handlers;

namespace Bicep.Extension.Helm.Tests;

[TestClass]
public sealed class ReleaseHandlerTests
{
    private static readonly object MinimalRelease = new
    {
        name = "azure-vote",
        repository = "https://azure-samples.github.io/helm-charts",
        chart = "azure-vote",
    };

    [TestMethod]
    public async Task CreateOrUpdate_runs_a_version_preflight_before_upgrading()
    {
        var helm = new FakeHelmCommandRunner();

        var response = await HandlerHarness.CreateOrUpdateAsync(new ReleaseHandler(helm), "Release", MinimalRelease);

        Assert.IsNull(response.ErrorData);
        CollectionAssert.AreEqual(new[] { "version", "--short" }, helm.Invocations[0].ToArray());
        Assert.AreEqual("upgrade", helm.Invocations[1][0]);
    }

    [TestMethod]
    public async Task CreateOrUpdate_upgrades_and_installs_the_chart_from_the_repository()
    {
        var helm = new FakeHelmCommandRunner();

        await HandlerHarness.CreateOrUpdateAsync(new ReleaseHandler(helm), "Release", MinimalRelease);

        CollectionAssert.AreEqual(
            new[]
            {
                "upgrade",
                "--install",
                "azure-vote",
                "azure-vote",
                "--repo",
                "https://azure-samples.github.io/helm-charts",
                "--kubeconfig",
                helm.KubeConfigPaths.Single(),
            },
            helm.UpgradeArgs.ToArray());
    }

    [TestMethod]
    public async Task CreateOrUpdate_targets_the_configured_cluster_rather_than_the_ambient_context()
    {
        var helm = new FakeHelmCommandRunner();

        await HandlerHarness.CreateOrUpdateAsync(new ReleaseHandler(helm), "Release", MinimalRelease);

        CollectionAssert.Contains(helm.UpgradeArgs.ToArray(), "--kubeconfig");
        Assert.AreEqual(HandlerHarness.TestKubeConfigContent, helm.KubeConfigContents.Single());
    }

    [TestMethod]
    public async Task CreateOrUpdate_accepts_a_kubeconfig_that_is_not_base64_encoded()
    {
        var helm = new FakeHelmCommandRunner();

        await HandlerHarness.CreateOrUpdateAsync(
            new ReleaseHandler(helm),
            "Release",
            MinimalRelease,
            kubeConfig: HandlerHarness.TestKubeConfigContent);

        Assert.AreEqual(HandlerHarness.TestKubeConfigContent, helm.KubeConfigContents.Single());
    }

    [TestMethod]
    public async Task CreateOrUpdate_accepts_base64_that_is_wrapped_across_lines()
    {
        // GNU coreutils' `base64` wraps at 76 columns, so the sample's documented command produces
        // a multi-line value on Linux.
        var wrapped = string.Join(
            '\n',
            Convert.ToBase64String(Encoding.UTF8.GetBytes(HandlerHarness.TestKubeConfigContent))
                .Chunk(20)
                .Select(chunk => new string(chunk)));
        var helm = new FakeHelmCommandRunner();

        await HandlerHarness.CreateOrUpdateAsync(new ReleaseHandler(helm), "Release", MinimalRelease, kubeConfig: wrapped);

        Assert.AreEqual(HandlerHarness.TestKubeConfigContent, helm.KubeConfigContents.Single());
    }

    [TestMethod]
    public async Task CreateOrUpdate_removes_the_kubeconfig_file_once_helm_has_run()
    {
        var helm = new FakeHelmCommandRunner();

        await HandlerHarness.CreateOrUpdateAsync(new ReleaseHandler(helm), "Release", MinimalRelease);

        Assert.IsFalse(File.Exists(helm.KubeConfigPaths.Single()), "The kubeconfig file should not outlive the deployment.");
    }

    [TestMethod]
    public async Task CreateOrUpdate_removes_the_kubeconfig_file_when_helm_fails()
    {
        var helm = new FakeHelmCommandRunner(args =>
            args is ["upgrade", ..] ? new InvalidOperationException("Helm command failed with exit code 1.") : null);

        var response = await HandlerHarness.CreateOrUpdateAsync(new ReleaseHandler(helm), "Release", MinimalRelease);

        Assert.IsNotNull(response.ErrorData);
        Assert.IsFalse(File.Exists(helm.KubeConfigPaths.Single()));
    }

    [TestMethod]
    public async Task CreateOrUpdate_writes_the_kubeconfig_as_a_user_only_file()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Unix file modes do not apply on Windows.");
            return;
        }

        UnixFileMode? mode = null;
        var helm = new FakeHelmCommandRunner(args =>
        {
            var index = Array.IndexOf(args.ToArray(), "--kubeconfig");
            if (index >= 0 && !OperatingSystem.IsWindows())
            {
                mode = File.GetUnixFileMode(args[index + 1]);
            }

            return null;
        });

        await HandlerHarness.CreateOrUpdateAsync(new ReleaseHandler(helm), "Release", MinimalRelease);

        Assert.AreEqual(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty kubeConfig")]
    [DataRow("   ", DisplayName = "whitespace kubeConfig")]
    public async Task CreateOrUpdate_rejects_an_empty_kubeConfig(string kubeConfig)
    {
        var helm = new FakeHelmCommandRunner();

        var response = await HandlerHarness.CreateOrUpdateAsync(
            new ReleaseHandler(helm),
            "Release",
            MinimalRelease,
            kubeConfig: kubeConfig);

        var error = response.ExpectError();
        Assert.AreEqual("InvalidConfiguration", error.Code);
        Assert.AreEqual(0, helm.Invocations.Count, "An invalid configuration should be rejected before invoking Helm.");
    }

    [TestMethod]
    public async Task CreateOrUpdate_creates_the_namespace_when_one_is_specified()
    {
        var helm = new FakeHelmCommandRunner();

        await HandlerHarness.CreateOrUpdateAsync(
            new ReleaseHandler(helm),
            "Release",
            new
            {
                name = "azure-vote",
                repository = "https://azure-samples.github.io/helm-charts",
                chart = "azure-vote",
                @namespace = "voting",
            });

        CollectionAssert.IsSubsetOf(
            new[] { "--namespace", "voting", "--create-namespace" },
            helm.UpgradeArgs.ToArray());
    }

    [TestMethod]
    public async Task CreateOrUpdate_passes_optional_version_and_values_file()
    {
        var helm = new FakeHelmCommandRunner();

        await HandlerHarness.CreateOrUpdateAsync(
            new ReleaseHandler(helm),
            "Release",
            new
            {
                name = "azure-vote",
                repository = "https://azure-samples.github.io/helm-charts",
                chart = "azure-vote",
                version = "0.1.2",
                valuesFile = "./values.yaml",
            });

        var args = helm.UpgradeArgs.ToArray();
        CollectionAssert.IsSubsetOf(new[] { "--version", "0.1.2" }, args);
        CollectionAssert.IsSubsetOf(new[] { "--values", "./values.yaml" }, args);
    }

    [TestMethod]
    public async Task CreateOrUpdate_expands_each_set_override_into_a_name_value_pair()
    {
        var helm = new FakeHelmCommandRunner();

        await HandlerHarness.CreateOrUpdateAsync(
            new ReleaseHandler(helm),
            "Release",
            new
            {
                name = "azure-vote",
                repository = "https://azure-samples.github.io/helm-charts",
                chart = "azure-vote",
                set = new[]
                {
                    new { name = "title", value = "Do you love Bicep?" },
                    new { name = "image.tag", value = "v1" },
                },
            });

        var args = helm.UpgradeArgs.ToArray();
        CollectionAssert.IsSubsetOf(new[] { "--set", "title=Do you love Bicep?" }, args);
        CollectionAssert.IsSubsetOf(new[] { "--set", "image.tag=v1" }, args);
        Assert.AreEqual(2, args.Count(arg => arg == "--set"));
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty namespace")]
    [DataRow("   ", DisplayName = "whitespace namespace")]
    public async Task CreateOrUpdate_ignores_blank_optional_values(string blank)
    {
        var helm = new FakeHelmCommandRunner();

        await HandlerHarness.CreateOrUpdateAsync(
            new ReleaseHandler(helm),
            "Release",
            new
            {
                name = "azure-vote",
                repository = "https://azure-samples.github.io/helm-charts",
                chart = "azure-vote",
                @namespace = blank,
                version = blank,
                valuesFile = blank,
            });

        var args = helm.UpgradeArgs.ToArray();
        CollectionAssert.DoesNotContain(args, "--namespace");
        CollectionAssert.DoesNotContain(args, "--version");
        CollectionAssert.DoesNotContain(args, "--values");
    }

    [TestMethod]
    public async Task CreateOrUpdate_returns_the_release_name_as_the_identifier()
    {
        var helm = new FakeHelmCommandRunner();

        var response = await HandlerHarness.CreateOrUpdateAsync(new ReleaseHandler(helm), "Release", MinimalRelease);

        Assert.AreEqual("azure-vote", response.ResourceIdentifiers().GetProperty("name").GetString());
        Assert.AreEqual("azure-vote", response.ResourceProperties().GetProperty("chart").GetString());
        Assert.AreEqual("Release", response.Resource.Type);
    }

    [TestMethod]
    public async Task CreateOrUpdate_reports_a_missing_helm_cli_without_attempting_an_upgrade()
    {
        var helm = new FakeHelmCommandRunner(_ =>
            new InvalidOperationException("Unable to start 'helm'. Ensure Helm CLI is installed and available on PATH."));

        var response = await HandlerHarness.CreateOrUpdateAsync(new ReleaseHandler(helm), "Release", MinimalRelease);

        var error = response.ExpectError();
        StringAssert.Contains(error.Message, "Ensure Helm CLI is installed");
        Assert.AreEqual(1, helm.Invocations.Count, "The upgrade should not run once the preflight fails.");
    }

    [TestMethod]
    public async Task CreateOrUpdate_surfaces_a_failed_upgrade_as_an_error()
    {
        var helm = new FakeHelmCommandRunner(args =>
            args is ["upgrade", ..] ? new InvalidOperationException("Helm command failed with exit code 1.") : null);

        var response = await HandlerHarness.CreateOrUpdateAsync(new ReleaseHandler(helm), "Release", MinimalRelease);

        StringAssert.Contains(response.ExpectError().Message, "Helm command failed with exit code 1.");
    }

    [TestMethod]
    public void BuildUpgradeArgs_orders_optional_flags_after_the_chart_reference()
    {
        var args = ReleaseHandler.BuildUpgradeArgs(
            new Release
            {
                Name = "azure-vote",
                Repository = "https://azure-samples.github.io/helm-charts",
                Chart = "azure-vote",
                Namespace = "voting",
                Version = "0.1.2",
                ValuesFile = "./values.yaml",
                Set = [new HelmSetValue { Name = "title", Value = "Bicep" }],
            },
            "/tmp/kubeconfig").ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "upgrade",
                "--install",
                "azure-vote",
                "azure-vote",
                "--repo",
                "https://azure-samples.github.io/helm-charts",
                "--kubeconfig",
                "/tmp/kubeconfig",
                "--namespace",
                "voting",
                "--create-namespace",
                "--version",
                "0.1.2",
                "--values",
                "./values.yaml",
                "--set",
                "title=Bicep",
            },
            args);
    }

    [TestMethod]
    public async Task CreateOrUpdate_rejects_properties_that_omit_required_values()
    {
        var helm = new FakeHelmCommandRunner();

        var response = await HandlerHarness.CreateOrUpdateAsync(
            new ReleaseHandler(helm),
            "Release",
            new { name = "azure-vote" });

        Assert.IsNotNull(response.ErrorData);
        Assert.AreEqual(0, helm.Invocations.Count);
    }

    [TestMethod]
    public void Release_properties_are_serialized_as_camelCase()
    {
        var json = JsonSerializer.Serialize(
            new Release
            {
                Name = "azure-vote",
                Repository = "https://azure-samples.github.io/helm-charts",
                Chart = "azure-vote",
                ValuesFile = "./values.yaml",
            },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        StringAssert.Contains(json, "\"valuesFile\":");
    }
}
