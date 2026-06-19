using Microsoft.AspNetCore.Builder;
using Bicep.Local.Extension.Host.Extensions;
using Bicep.Extension.Helm.Handlers;
using Azure.Bicep.Types.Concrete;
using Microsoft.Extensions.DependencyInjection;
using Bicep.Extension.Helm;

var builder = WebApplication.CreateBuilder();

builder.AddBicepExtensionHost(args);
builder.Services
    .AddBicepExtension(
        name: ThisAssembly.AssemblyName.Split('-')[^1],
        version: ThisAssembly.AssemblyInformationalVersion.Split('+')[0],
        isSingleton: true,
        typeAssembly: typeof(Program).Assembly,
        configurationType: typeof(Configuration))
    .WithResourceHandler<ReleaseHandler>();

var app = builder.Build();
app.MapBicepExtension();

await app.RunAsync();