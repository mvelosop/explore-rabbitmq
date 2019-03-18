using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace GenericHostSample
{
    public class Program
    {
        public static readonly string AppName = typeof(Program).Namespace;

        public static async Task<int> Main(string[] args)
        {
            try
            {
                //Log.Information("Configuring host ({ApplicationContext})...", AppName);
                //var host = BuildHost(configuration, args);

                //LogPackagesVersionInfo();

                //Log.Information("Starting host ({ApplicationContext})...", AppName);
                //await host.RunAsync();

                //return 0;

                var host = new HostBuilder()
                    .ConfigureHostConfiguration(configHost =>
                    {
                        Console.WriteLine("Setting up host configuration...");

                        configHost.SetBasePath(Directory.GetCurrentDirectory())
                            .AddJsonFile("hostsettings.json", optional: true)
                            .AddEnvironmentVariables(prefix: "PREFIX_")
                            .AddCommandLine(args);
                    })
                    .ConfigureAppConfiguration((hostContext, configurationBuilder) =>
                    {
                        Console.WriteLine("Setting up app configuration...");

                        configurationBuilder.AddJsonFile("appsettings.json", optional: true)
                            .AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", optional: true)
                            .AddEnvironmentVariables(prefix: "PREFIX_")
                            .AddCommandLine(args);
                    })
                    .ConfigureServices((hostContext, services) =>
                    {
                        Console.WriteLine("Setting up app configuration...");

                        ConfigureServices(hostContext, services);
                    })
                    .ConfigureLogging((hostContext, loggingBuilder) =>
                    {
                        Console.WriteLine("Setting up logging...");

                        var seqServerUrl = hostContext.Configuration["Serilog:SeqServerUrl"];

                        Log.Logger = new LoggerConfiguration()
                            .MinimumLevel.Verbose()
                            .Enrich.WithProperty("ApplicationContext", AppName)
                            .Enrich.WithThreadId()
                            .Enrich.FromLogContext()
                            .WriteTo.Console()
                            .WriteTo.Seq(string.IsNullOrWhiteSpace(seqServerUrl) ? "http://localhost:5341/" : seqServerUrl)
                            .ReadFrom.Configuration(hostContext.Configuration)
                            .CreateLogger();

                        loggingBuilder.Services.AddSingleton(new LoggerFactory().AddSerilog());
                        loggingBuilder.Services.AddLogging();
                    })
                    .UseConsoleLifetime()
                    .Build();

                await host.RunAsync();

                return 0;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Program terminated unexpectedly ({ApplicationContext})!", AppName);
                return 1;
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static void ConfigureServices(HostBuilderContext hostContext, IServiceCollection services)
        {
            services.AddHostedService<LifetimeEventsHostedService>();
            services.AddHostedService<TimedHostedService>();
        }

        private static string GetVersion(Assembly assembly)
        {
            try
            {
                return $"{assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version} ({assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split()[0]})";
            }
            catch
            {
                return string.Empty;
            }
        }

        private static void LogPackagesVersionInfo()
        {
            var assemblies = new List<Assembly>();

            foreach (var assemblyName in typeof(Program).Assembly.GetReferencedAssemblies())
            {
                try
                {
                    // Try to load the referenced assembly...
                    assemblies.Add(Assembly.Load(assemblyName));
                }
                catch
                {
                    // Failed to load assembly. Skip it.
                }
            }

            var versionList = assemblies.Select(a => $"-{a.GetName().Name} - {GetVersion(a)}").OrderBy(value => value);

            Log.Logger.ForContext("PackageVersions", string.Join("\n", versionList)).Information("Package versions ({ApplicationContext})", AppName);
        }
    }
}