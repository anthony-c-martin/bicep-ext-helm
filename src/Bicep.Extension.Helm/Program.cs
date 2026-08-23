using Microsoft.AspNetCore.Builder;
using Bicep.Local.Extension.Host.Extensions;
using Bicep.Extension.Helm.Handlers;
using Azure.Bicep.Types.Concrete;
using Microsoft.Extensions.DependencyInjection;
using Bicep.Extension.Helm;
using System.Reflection;

var assembly = typeof(Program).Assembly;
var assemblyName = assembly.GetName().Name ?? "bicep-ext-helm";
var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? assembly.GetName().Version?.ToString()
    ?? "0.0.0";

var builder = WebApplication.CreateBuilder();

builder.AddBicepExtensionHost(args);
builder.Services.AddSingleton<IHelmCommandRunner, HelmCommandRunner>();
builder.Services
    .AddBicepExtension()
    .WithDefaults(
        assemblyName.Split('-')[^1],
        informationalVersion.Split('+')[0],
        isSingleton: true)
    .WithTypeAssembly(typeof(Program).Assembly)
    .WithConfigurationType<Configuration>()
    .WithResourceHandler<ReleaseHandler>();

var app = builder.Build();
app.MapBicepExtension();

await app.RunAsync();