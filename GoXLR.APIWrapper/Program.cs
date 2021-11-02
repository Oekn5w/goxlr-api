using System;
using System.IO;
using GoXLR.Server;
using GoXLR.Server.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GoXLR.APIWrapper
{
   class Program
   {
      static void Main(string[] args)
      {
         var configurationRoot = new ConfigurationBuilder()
             .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
             .AddJsonFile("appsettings.json")
             .Build();

         var serviceCollection = new ServiceCollection();

         //Add Logging:
         serviceCollection.AddLogging(configure =>
         {
            configure.AddSimpleConsole(options => options.TimestampFormat = "[yyyy-MM-ddTHH:mm:ss] ");
            configure.AddConfiguration(configurationRoot.GetSection("Logging"));
         });

         var logger = serviceCollection
             .BuildServiceProvider()
             .GetRequiredService<ILogger<Program>>();

         //Add AppSettings:
         serviceCollection.Configure<AppSettings>(configurationRoot);

         //Add WebSocket Server:
         serviceCollection.Configure<WebSocketServerSettings>(configurationRoot.GetSection("WebSocketServerSettings"));
         serviceCollection.AddSingleton<GoXLRServer>();

         var serviceProvider = serviceCollection.BuildServiceProvider();

         logger.LogInformation("Initializing GoXLR Server");
         var goXlrServer = serviceProvider.GetRequiredService<GoXLRServer>();
         goXlrServer.Init();
         logger.LogInformation("GoXLR Server initialized");

         Console.ReadLine();

      }
   }
}
