using System.Collections.Generic;
using System.IO;

using Autofac.Extensions.DependencyInjection;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ASC.Web.Studio
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateWebHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateWebHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureWebHostDefaults(w =>
                {
                    w.UseStartup<Startup>();
                })
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    var buided = config.Build();
                    var path = buided["pathToConf"];
                    if (!Path.IsPathRooted(path))
                    {
                        path = Path.GetFullPath(Path.Combine(hostingContext.HostingEnvironment.ContentRootPath, path));
                    }
                    config.SetBasePath(path);
                    config
                    .AddInMemoryCollection(new Dictionary<string, string>
                    {
                        {"pathToConf", path}
                    })
                    .AddJsonFile("appsettings.json")
                    .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", true)
                    .AddJsonFile("storage.json")
                    .AddJsonFile("kafka.json")
                    .AddJsonFile($"kafka.{hostingContext.HostingEnvironment.EnvironmentName}.json", true)
                    .AddEnvironmentVariables()
                    .AddCommandLine(args);
                });
        }
    }
}
