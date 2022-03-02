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
         //builder.Services.Configure<KestrelServerOptions>(options => { options.AllowSynchronousIO = true; });
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
                  switch (profileEle.ValueKind)
                  {
                     case JsonValueKind.Number:
                        tempIdx = profileEle.GetInt32();
                        if (tempIdx >= 0 && tempIdx < profiles.Count())
                        {
                           reqProfile = profiles[tempIdx];
                           success = true;
                        }
                        break;
                     case JsonValueKind.String:
                        reqProfile = profileEle.GetString();
                        success = profiles.Contains(reqProfile);
                        break;
                     default:
                        information = "Given profile identifier not valid.";
                        logger.LogInformation("MapPOST: SetProfile: Invalid profile identifier supplied.");
                        break;
                  }
                  if (success)
                  {
                     goXlrServer.SetProfile(reqProfile);
                     information = "New profile set.";
                     logger.LogInformation("MapPOST: SetProfile: New profile set.");
                  }
                  else
                  {
                     logger.LogInformation("MapPOST: SetProfile: Given profile identifier not found.");
                     information = "Given profile not in list of profiles.";
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

         app.MapPost("/routing", async context =>
         {
            handleRouting(ref goXlrServer, ref logger, ref context);
         });

         app.MapPost("/routing/set", async context =>
         {
            handleRouting(ref goXlrServer, ref logger, ref context);
         });

         app.MapPost("/routing/clear", async context =>
         {
            handleRouting(ref goXlrServer, ref logger, ref context);
         });

         app.MapPost("/routing/toggle", async context =>
         {
            handleRouting(ref goXlrServer, ref logger, ref context);
         });

         app.Run();

      }

      static private string[] mappingAction = {
         "Turn On",
         "Turn Off",
         "Toggle"
      };
      static private Dictionary<string, int> dictAction =
         new Dictionary<string, int>() {
            {"0", 1 }, {"r", 1}, {"c", 1},
            {"1", 0 }, {"s", 0},
            {"t", 2 }
      };

      static private string[] mappingInput = {
         "Mic",
         "Chat",
         "Music",
         "Game",
         "Console",
         "Line In",
         "System",
         "Samples"
      };
      private static bool validateInput(int id) { return id >= 0 && id < mappingInput.Length; }

      static private string[] mappingOutput = {
         "Headphones",
         "Broadcast Mix",
         "Line Out",
         "Chat Mic",
         "Sampler"
      };
      private static bool validateOutput(int id) { return id >= 0 && id < mappingOutput.Length; }

      private static void handleRouting(ref GoXLRServer server, ref ILogger<Program> logger, ref HttpContext context)
      {
         context.Response.StatusCode = 200;
         context.Response.ContentType = "application/json";
         bool connected = server.GetConnected();
         bool success = false;
         string information = "disconnected";
         if (connected)
         {
            StreamReader sr = new StreamReader(context.Request.Body);
            Task<string> readtask = sr.ReadToEndAsync();
            readtask.Wait();
            sr.Close();
            logger.LogInformation(context.Request.Path.Value);
            logger.LogInformation(readtask.Result);
            try
            {
               string reqUrl = context.Request.Path.Value;
               int idAction = -1;
               List<int> idInputs = new List<int> { };
               List<int> idOutputs = new List<int> { };
               if (reqUrl.EndsWith("/set"))
               {
                  idAction = dictAction["s"];
               }
               else if (reqUrl.EndsWith("/clear"))
               {
                  idAction = dictAction["r"];
               }
               else if (reqUrl.EndsWith("/toggle"))
               {
                  idAction = dictAction["t"];
               }

               JsonElement JsonReq = JsonSerializer.Deserialize<JsonDocument>(readtask.Result).RootElement;
               if (idAction == -1)
               {
                  try
                  {
                     var actionEle = JsonReq.GetProperty("action");
                     string strAction = "";
                     switch (actionEle.ValueKind)
                     {
                        case JsonValueKind.Number:
                           strAction = Convert.ToString(actionEle.GetInt32());
                           break;
                        case JsonValueKind.String:
                           strAction = actionEle.GetString();
                           break;
                        default:
                           break;
                     }
                     if (strAction != "" && dictAction.ContainsKey(strAction))
                     {
                        idAction = dictAction[strAction];
                     }
                  }
                  catch (Exception ex) { }
               }
               try
               {
                  var inputEleItem = JsonReq.GetProperty("input");
                  List<JsonElement> inputEleList = new List<JsonElement>();
                  if (inputEleItem.ValueKind == JsonValueKind.Array)
                  {
                     var enumerator = inputEleItem.EnumerateArray();
                     inputEleList = enumerator.ToList();
                  }
                  else
                  {
                     inputEleList.Add(inputEleItem);
                  }
                  int id;
                  string strInput;
                  foreach (var inputEle in inputEleList)
                  {
                     switch (inputEle.ValueKind)
                     {
                        case JsonValueKind.Number:
                           id = inputEle.GetInt32();
                           if (validateInput(id) && !idInputs.Contains(id))
                           {
                              idInputs.Add(id);
                           }
                           break;
                        case JsonValueKind.String:
                           strInput = inputEle.GetString();
                           if (mappingInput.Contains(strInput))
                           {
                              id = Array.IndexOf(mappingInput, strInput);
                              if (!idInputs.Contains(id))
                              {
                                 idInputs.Add(id);
                              }
                           }
                           break;
                        default:
                           break;
                     }
                  }
               }
               catch (Exception ex) { }
               try
               {
                  var outputEleItem = JsonReq.GetProperty("output");
                  List<JsonElement> outputEleList = new List<JsonElement>();
                  if (outputEleItem.ValueKind == JsonValueKind.Array)
                  {
                     var enumerator = outputEleItem.EnumerateArray();
                     outputEleList = enumerator.ToList();
                  }
                  else
                  {
                     outputEleList.Add(outputEleItem);
                  }
                  int id;
                  string strOutput;
                  foreach (var outputEle in outputEleList)
                  {
                     switch (outputEle.ValueKind)
                     {
                        case JsonValueKind.Number:
                           id = outputEle.GetInt32();
                           if (validateOutput(id) && !idOutputs.Contains(id))
                           {
                              idOutputs.Add(id);
                           }
                           break;
                        case JsonValueKind.String:
                           strOutput = outputEle.GetString();
                           if (mappingOutput.Contains(strOutput))
                           {
                              id = Array.IndexOf(mappingOutput, strOutput);
                              if (!idOutputs.Contains(id))
                              {
                                 idOutputs.Add(id);
                              }
                           }
                           break;
                        default:
                           break;
                     }
                  }
               }
               catch (Exception ex) { }
               if (idAction != -1 && idInputs.Count > 0 && idOutputs.Count > 0)
               {
                  success = true;
                  foreach (var idOutput in idOutputs)
                  {
                     foreach (var idInput in idInputs)
                     {
                        string temp;
                        success &= server.SetRouting(mappingAction[idAction], mappingInput[idInput], mappingOutput[idOutput], out temp);
                     }
                  }
               }
               else
               {
                  information = "Not enough data provided.";
               }
            }
            catch (Exception ex)
            {
               logger.LogInformation("MapPOST: Routing: Invalid JSON data supplied.");
               logger.LogInformation(ex.ToString());
               information = "JSON data not valid.";
            }
         }
         JsonObject response = new JsonObject
         {
            ["status"] = success,
            ["information"] = information
         };
         var task = context.Response.Body.WriteAsync(Encoding.UTF8.GetBytes(response.ToJsonString()));
         while (!task.IsCompleted) { }
      }
   }

   public class AppSettings
   {
      public WebSocketServerSettings WebSocketServerSettings { get; set; }
   }
}
