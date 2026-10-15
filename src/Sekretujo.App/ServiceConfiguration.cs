using System;
using Microsoft.Extensions.DependencyInjection;
using Sekretujo.App.Services;
using Sekretujo.App.ViewModels;
using Sekretujo.Core.Crypto;
using Sekretujo.Core.Services;
using Sekretujo.Core.Storage;

namespace Sekretujo.App;

public static class ServiceConfiguration
{
    public static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<CryptoService>();
        services.AddSingleton<VaultSerializer>();
        services.AddSingleton<VaultRepository>();
        services.AddSingleton<PasswordGeneratorService>();
        services.AddSingleton<TotpService>();
        services.AddSingleton<ImportExportService>();
        services.AddSingleton<VaultSession>();
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
