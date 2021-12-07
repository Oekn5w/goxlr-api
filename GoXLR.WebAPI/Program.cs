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
using System.Text.Json.Nodes;
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
            JsonObject response = new JsonObject
            {
               ["statusAPI"] = "GoXLR API online!"
            };
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(response.ToJsonString()));
         });

         app.MapGet("/status", async context =>
         {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            bool connected = goXlrServer.GetConnected();
            JsonObject response = new JsonObject
            {
               ["status"] = connected,
               ["information"] = connected ? "connected" : "disconnected"
            };
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(response.ToJsonString()));
         });

         app.MapGet("/profilenames", async context =>
         {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            bool connected = goXlrServer.GetConnected();
            JsonObject response = new JsonObject
            {
               ["status"] = connected
            };
            if (connected)
            {
               var profiles = goXlrServer.GetProfiles(true);
               var tempjsonarr = new JsonArray();
               foreach (var profile in profiles)
               {
                  tempjsonarr.Add(profile);
               }
               response["profiles"] = tempjsonarr;
            }
            else
            {
               response["information"] = "disconnected";
            }
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(response.ToJsonString()));
         });

         app.MapPost("/profile/set", async context =>
         {
            context.Response.StatusCode = 200;
            context.Response.ContentType = "application/json";
            bool connected = goXlrServer.GetConnected();
            bool success = false;
            string information = "disconnected";
            if (connected)
            {
               StreamReader sr = new StreamReader(context.Request.Body);
               Task<string> readtask = sr.ReadToEndAsync();
               readtask.Wait();
               sr.Close();
               logger.LogInformation(readtask.Result);
               try
               {
                  JsonElement JsonReq = JsonSerializer.Deserialize<JsonDocument>(readtask.Result).RootElement;
                  var profileEle = JsonReq.GetProperty("profile");
                  int tempIdx;
                  string reqProfile = "";
                  var profiles = goXlrServer.GetProfiles(true);
                  try
                  {
                     tempIdx = profileEle.GetInt32();
                     if (tempIdx >= 0 && tempIdx < profiles.Count())
                     {
                        reqProfile = profiles[tempIdx];
                        success = true;
                     }
                  }
                  catch (Exception ex) { }
                  if (reqProfile == "")
                  {
                     try
                     {
                        reqProfile = profileEle.GetString();
                        success = profiles.Contains(reqProfile);
                        if (!success)
                        {
                           logger.LogInformation("MapPOST: SetProfile: Given profile identifier not found.");
                           information = "Given profile not in list of profiles.";
                        }
                     }
                     catch (Exception ex)
                     {
                        information = "Given profile identifier not valid.";
                        logger.LogInformation("MapPOST: SetProfile: Invalid profile identifier supplied.");
                     }
                  }
                  if (success)
                  {
                     goXlrServer.SetProfile(reqProfile);
                     information = "New profile set.";
                     logger.LogInformation("MapPOST: SetProfile: New profile set.");
                  }
               }
               catch (Exception ex)
               {

                  logger.LogInformation("MapPOST: SetProfile: Invalid JSON data supplied.");
                  logger.LogInformation(ex.ToString());
                  information = "JSON data not valid.";
               }
            }
            JsonObject response = new JsonObject
            {
               ["status"] = success,
               ["information"] = information
            };
            await context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(response.ToJsonString()));
         });

         app.Run();

      }
      private bool setRouting(ref GoXLRServer server, string action, string input, string output)
      {
         return false;
      }
   }

   public class AppSettings
   {
      public WebSocketServerSettings WebSocketServerSettings { get; set; }
   }
}
