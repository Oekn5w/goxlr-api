using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GoXLR.Server;
using GoXLR.Server.Models;

namespace GoXLR.WebAPI
{
   public class Program
   {
      public static void Main(string[] args)
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

         var builder = WebApplication.CreateBuilder(args);
         var app = builder.Build();

         app.MapGet("/", async context =>
         {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes($"{{\"statusAPI\":{JsonSerializer.Serialize("GoXLR API online!")}}}"));
            //return Task.CompletedTask;
         });

         app.MapGet("/status", async context =>
         {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            string response = goXlrServer.GetConnected() ? "connected" : "disconnected";
            response = $"{{\"status\":{JsonSerializer.Serialize(response)}}}";
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(response));
         });

         app.MapGet("/profilenames", async context =>
         {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            bool connected = goXlrServer.GetConnected();
            string response = "";
            if (connected)
            {
               var profiles = goXlrServer.GetProfiles();
               response = $"{{\"success\":{connected.ToString()},\"profiles\":{JsonSerializer.Serialize(profiles.ToArray())}}}";
            }
            else
            {
               response = $"{{\"success\":{connected.ToString()}}}";
            }
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(response));
         });

         app.Run();

      }
      private string setRouting(ref GoXLRServer server, string action, string input, string output)
      {
         return "";
      }
   }

   public class AppSettings
   {
      public WebSocketServerSettings WebSocketServerSettings { get; set; }
   }
}
