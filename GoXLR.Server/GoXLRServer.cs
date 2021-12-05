using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Fleck;
using GoXLR.Server.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GoXLR.Server
{
   // ReSharper disable InconsistentNaming
   [System.Diagnostics.CodeAnalysis.SuppressMessage("Minor Code Smell", "S101:Types should be named in PascalCase", Justification = "This is they we write it.")]
   public class GoXLRServer
   {
      private readonly ILogger _logger;
      private readonly WebSocketServerSettings _settings;

      private IWebSocketConnection _connection;
      private ClientIdentifier _identifier;
      private bool _clientConnected;
      private List<string> _profiles;
      private string[] tempArr;
      private bool _inProgress;

      public GoXLRServer(ILogger<GoXLRServer> logger, IOptions<WebSocketServerSettings> options)
      {
         _logger = logger;
         _settings = options.Value;
         _clientConnected = false;
         _inProgress = false;
         _connection = null;
         _identifier = null;
         _profiles = new();
      }

      /// <summary>
      /// Starts the WebSockets server.
      /// </summary>
      public void Init()
      {
         _clientConnected = false;
         var server = new WebSocketServer($"ws://{_settings.IpAddress}:{_settings.Port}/?GOXLRApp");
         server.Start(OnClientConnecting);
      }

      /// <summary>
      /// Setting up Fleck WebSocket callbacks.
      /// </summary>
      /// <param name="socket"></param>
      private void OnClientConnecting(IWebSocketConnection socket)
      {
         var connectionInfo = socket.ConnectionInfo;
         var identifier = new ClientIdentifier(connectionInfo.ClientIpAddress, connectionInfo.ClientPort);

         socket.OnOpen = () =>
         {
            _clientConnected = true;
            _connection = socket;
            _identifier = identifier;
            _logger.LogInformation($"Connection opened {socket.ConnectionInfo.ClientIpAddress}:{socket.ConnectionInfo.ClientPort}.");

            //Get updated profiles:
            _profiles = FetchProfiles();
         };

         socket.OnClose = () =>
         {
            if (_identifier == identifier)
            {
               _connection = null;
               _clientConnected = false;
               _identifier = null;
               _profiles = null;
            }
            _logger.LogInformation($"Connection closed {socket.ConnectionInfo.ClientIpAddress}.");
         };

         socket.OnBinary = (bytes) =>
         {
            try
            {
               var message = Encoding.UTF8.GetString(bytes);
               var action = socket.OnMessage;
               if (action == null)
               {
                  _logger.LogWarning("Binary: OnMessage not registered.");
               }
               else
               {
                  action(message);
               }
            }
            catch (Exception e)
            {
               _logger.LogError(e.ToString());
            }
         };

         socket.OnMessage = (message) =>
         {
            _inProgress = true;
            _logger.LogDebug("Message: {0}", message);

            try
            {
               var document = JsonSerializer.Deserialize<JsonDocument>(message);
               if (document is null)
               {
                  _inProgress = false;
                  return;
               }

               var root = document.RootElement;
               var propertyAction = root.GetProperty("action").GetString();
               var propertyEvent = root.GetProperty("event").GetString();

               var changeProfileActions = new[]
                  {
                        "com.tchelicon.goxlr.profilechange", //SD plugin v0.17+
                        "com.tchelicon.goXLR.ChangeProfile" //obsolete pre-0.17
               };

               if (changeProfileActions.Contains(propertyAction) &&
                      propertyEvent == "sendToPropertyInspector")
               {
                  //Format:
                  tempArr = root
                      .GetProperty("payload")
                      .GetProperty("Profiles")
                      .EnumerateArray()
                      .Select(element => element.GetString())
                      .ToArray();

               }
               else
               {
                  _logger.LogWarning("Unknown contextId from GoXLR.");
               }
            }
            catch (Exception e)
            {
               _logger.LogError(e.ToString());
            }
            _inProgress = false;
         };

         socket.OnError = (exception) => _logger.LogError(exception.ToString());
      }

      /// <summary>
      /// Fetching profiles from the selected GoXLR App.
      /// </summary>
      private List<string> FetchProfiles()
      {
         if (!_clientConnected)
         {
            _logger.LogWarning($"No client currently connected");
            return new();
         }

         //Sanitize:
         var contextId = JsonSerializer.Serialize($"{_identifier.ClientIpAddress}:{_identifier.ClientPort}");

         //Build:
         var json = $"{{\"action\":\"com.tchelicon.goxlr.profilechange\",\"context\":{contextId},\"event\":\"propertyInspectorDidAppear\"}}";

         tempArr = new string[0];
         _inProgress = true;

         //Send:
         _ = _connection.Send(json);

         while (_inProgress) { }

         return tempArr.ToList();
      }

      public List<string> GetProfiles()
      {
         _profiles = FetchProfiles();
         return _profiles;
      }

      public bool GetConnected()
      {
         return _clientConnected;
      }

      /// <summary>
      /// Sets a profile in the selected GoXLR App.
      /// </summary>
      /// <param name="clientIdentifier"></param>
      /// <param name="profileName"></param>
      public void SetProfile(string profileName)
      {
         if (!_clientConnected)
         {
            _logger.LogWarning($"No client currently connected");
            return;
         }

         //Sanitize:
         profileName = JsonSerializer.Serialize(profileName);

         //Build:
         var json = $"{{\"action\":\"com.tchelicon.goxlr.profilechange\",\"event\":\"keyUp\",\"payload\":{{\"settings\":{{\"SelectedProfile\":{profileName}}}}}}}";

         //Send:
         _ = _connection.Send(json);
      }

      /// <summary>
      /// Sets a routing in the selected GoXLR App.
      /// </summary>
      /// <param name="clientIdentifier"></param>
      /// <param name="action"></param>
      /// <param name="input"></param>
      /// <param name="output"></param>
      public bool SetRouting(string action, string input, string output, out string reason)
      {
         reason = "";
         if (!_clientConnected)
         {
            reason = "No Client connected.";
            _logger.LogWarning(reason);
            return false;
         }

         string[] possibleActions = {
            "Turn On",
            "Turn Off",
            "Toggle"
         };

         string[] possibleInputs = {
            "Mic",
            "Chat",
            "Music",
            "Game",
            "Console",
            "Line In",
            "System",
            "Samples"
         };

         string[] possibleOutputs = {
            "Headphones",
            "Broadcast Mix",
            "Line Out",
            "Chat Mic",
            "Sampler"
         };

         if (!possibleActions.Contains(action) || !possibleInputs.Contains(input) || !possibleOutputs.Contains(output))
         {
            reason = "Invalid string input.";
            _logger.LogInformation(reason);
            return false;
         }

         //Sanitize:
         action = JsonSerializer.Serialize(action);
         input = JsonSerializer.Serialize(input);
         output = JsonSerializer.Serialize(output);

         //Build:
         var json = $"{{\"action\":\"com.tchelicon.goxlr.routingtable\",\"event\":\"keyUp\",\"payload\":{{\"settings\":{{\"RoutingAction\":{action},\"RoutingInput\":{input},\"RoutingOutput\":{output}}}}}}}";

         //Send:
         _ = _connection.Send(json);

         return true;
      }
   }
}
