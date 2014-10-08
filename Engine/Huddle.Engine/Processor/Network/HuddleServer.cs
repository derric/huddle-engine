﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Huddle.Engine.Data;
using Huddle.Engine.Properties;
using Huddle.Engine.Util;
using Newtonsoft.Json;
using Fleck;

namespace Huddle.Engine.Processor.Network
{
    [ViewTemplate("Huddle Server", "HuddleServerTemplate")]
    public class HuddleServer : BaseProcessor
    {
        #region member fields

        private Stopwatch _stopwatch;

        private Fleck.WebSocketServer _webSocketServer;

        private readonly ConcurrentQueue<string> _deviceIdQueue = new ConcurrentQueue<string>();

        private readonly Dictionary<string, string> _deviceIdToGlyph = new Dictionary<string, string>();

        private readonly ConcurrentDictionary<Guid, FleckClient> _connectedClients = new ConcurrentDictionary<Guid, FleckClient>();

        private readonly Stopwatch _lastProximityUpdate = new Stopwatch();

        #endregion

        #region properties

        #region Port

        /// <summary>
        /// The <see cref="Port" /> property's name.
        /// </summary>
        public const string PortPropertyName = "Port";

        private int _port = 1948;

        /// <summary>
        /// Sets and gets the Port property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public int Port
        {
            get
            {
                return _port;
            }

            set
            {
                if (_port == value)
                {
                    return;
                }

                RaisePropertyChanging(PortPropertyName);
                _port = value;
                RaisePropertyChanged(PortPropertyName);
            }
        }

        #endregion

        #region OutgoingFps

        /// <summary>
        /// The <see cref="OutgoingFps" /> property's name.
        /// </summary>
        public const string OutgoingFpsPropertyName = "OutgoingFps";

        private int _outgoingFps = 30;

        /// <summary>
        /// Sets and gets the OutgoingFps property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public int OutgoingFps
        {
            get
            {
                return _outgoingFps;
            }

            set
            {
                if (_outgoingFps == value)
                {
                    return;
                }

                RaisePropertyChanging(OutgoingFpsPropertyName);
                _outgoingFps = value;
                RaisePropertyChanged(OutgoingFpsPropertyName);
            }
        }

        #endregion

        #region ClientCount

        /// <summary>
        /// The <see cref="ClientCount" /> property's name.
        /// </summary>
        public const string ClientCountPropertyName = "ClientCount";

        private int _clientCount;

        /// <summary>
        /// Sets and gets the ClientCount property.
        /// Changes to that property's value raise the PropertyChanged event. 
        /// </summary>
        public int ClientCount
        {
            get
            {
                return _clientCount;
            }

            set
            {
                if (_clientCount == value)
                {
                    return;
                }

                RaisePropertyChanging(ClientCountPropertyName);
                _clientCount = value;
                RaisePropertyChanged(ClientCountPropertyName);
            }
        }

        #endregion

        #endregion

        #region ctor

        public HuddleServer()
        {
            using (var reader = new StreamReader("Resources/TagDefinitions.txt"))
            {
                String line;
                do
                {
                    line = reader.ReadLine();
                    if (line != null)
                    {
                        var tokens = line.Split(' ');
                        var deviceId = tokens[0];
                        var code = tokens[1];

                        _deviceIdToGlyph.Add(deviceId, code);
                        _deviceIdQueue.Enqueue(deviceId);
                    }
                } while (line != null);
            }
        }

        #endregion

        public override void Start()
        {
            PropertyChanged += (sender, args) =>
            {
                switch (args.PropertyName)
                {
                    case PortPropertyName:
                        RestartWebSocketServer();
                        break;
                }
            };

            // start web socket server.
            StartWebSocketServer();

            base.Start();
        }

        public override void Stop()
        {
            // stop web socket server
            StopWebSocketServer();

            base.Stop();
        }

        /// <summary>
        /// Start web socket server.
        /// </summary>
        private void StartWebSocketServer()
        {
            // stop web socket server in case a server is already running.
            StopWebSocketServer();

            _lastProximityUpdate.Start();

            _webSocketServer = new Fleck.WebSocketServer(string.Format("ws://0.0.0.0:{0}", Port));
            _webSocketServer.Start(socket =>
            {
                socket.OnOpen = () => OnClientConnect(socket);
                socket.OnClose = () => OnClientDisconnect(socket);
                socket.OnMessage = message => OnClientMessage(socket, message);
            });
        }

        /// <summary>
        /// Stop web socket server.
        /// </summary>
        private void StopWebSocketServer()
        {
            // send disconnect to clients
            foreach (var client in _connectedClients.Values)
                client.Close();

            // reset client count
            ClientCount = 0;

            if (_webSocketServer != null)
                _webSocketServer.Dispose();

            _lastProximityUpdate.Stop();
        }

        /// <summary>
        /// Restart web socket server.
        /// </summary>
        private void RestartWebSocketServer()
        {
            StopWebSocketServer();
            StartWebSocketServer();
        }

        public override IData Process(IData data)
        {
            return data;
        }

        public override IDataContainer PreProcess(IDataContainer dataContainer)
        {
            var devices = dataContainer.OfType<Device>().ToArray();
            var identifiedDevices = devices.Where(d => d.IsIdentified).ToArray();

            var clients = _connectedClients.Values.ToArray();

            #region Reveal QrCode on unidentified clients

            var digital = new Digital(this, "Identify") { Value = true };
            foreach (var client in clients)
            {
                if (identifiedDevices.Any(d => Equals(d.DeviceId, client.Id)) ||
                    client.Id == null) continue;

                client.Send(digital);
            }

            var digital2 = new Digital(this, "Identify") { Value = false };
            foreach (var client in clients)
            {
                if (identifiedDevices.Any(d => Equals(d.DeviceId, client.Id)))
                {
                    client.Send(digital2);
                }
            }

            #endregion

            #region Send proximity information to clients

            var proximities = dataContainer.OfType<Proximity>().ToArray();

            // Calculate frames per second -> this speed defines the outgoing fps
            if (proximities.Any())
            {
                if (_stopwatch == null)
                {
                    _stopwatch = new Stopwatch();
                    _stopwatch.Start();
                }
                else
                {
                    Pipeline.Fps = 1000.0 / _stopwatch.ElapsedMilliseconds;
                    _stopwatch.Restart();
                }
            }

            if (_lastProximityUpdate.ElapsedMilliseconds > 1000 / OutgoingFps)
            {
                foreach (var proximity in proximities)
                {
                    var proximity1 = proximity;
                    foreach (var client in clients.Where(c => Equals(c.Id, proximity1.Identity)))
                    {
                        client.Send(proximity);
                    }
                }

                _lastProximityUpdate.Restart();
            }

            #endregion

            return null;
        }

        #region WebSocket Handling

        /// <summary>
        /// Handle client connection.
        /// </summary>
        /// <param name="socket">Client socket.</param>
        private void OnClientConnect(IWebSocketConnection socket)
        {
            var clientKey = socket.ConnectionInfo.Id;

            var client = new FleckClient(socket);

            _connectedClients.TryAdd(clientKey, client);
            ClientCount = _connectedClients.Count;

            // Log client connected message.
            var info = socket.ConnectionInfo;
            Log("Client {0}:{1} connected", info.ClientIpAddress, info.ClientPort);
        }

        /// <summary>
        /// Handle client disconnection.
        /// </summary>
        /// <param name="socket">Client socket.</param>
        private void OnClientDisconnect(IWebSocketConnection socket)
        {
            var clientKey = socket.ConnectionInfo.Id;

            FleckClient client;
            _connectedClients.TryRemove(clientKey, out client);
            ClientCount = _connectedClients.Count;

            if (client == null)
            {
                var info = socket.ConnectionInfo;
                Log("Client does exists for socket connection: {0}:{1}", info.ClientIpAddress, info.ClientPort);
                return;
            }

            // Log client disconnected message.
            Log("Client {0} [id={1}, deviceType={2}] disconnected", client.Name, client.Id, client.DeviceType);

            // Put unused device id back to queue.
            _deviceIdQueue.Enqueue(client.Id);

            // Notify tracking that client disconnected.
            Stage(new Disconnected(this, "Disconnect") { Value = client.Id });
            Push();
        }

        /// <summary>
        /// Handle incoming client messages.
        /// </summary>
        /// <param name="socket">Client socket.</param>
        /// <param name="message">Client message.</param>
        private void OnClientMessage(IWebSocketConnection socket, string message)
        {
            var clientKey = socket.ConnectionInfo.Id;

            FleckClient client;
            _connectedClients.TryGetValue(clientKey, out client);

            // check if client exists for the socket connection
            if (client == null)
            {
                var info = socket.ConnectionInfo;
                Log("Client does exists for socket connection: {0}:{1}", info.ClientIpAddress, info.ClientPort);
                return;
            }

            try
            {
                dynamic response = JsonConvert.DeserializeObject(message);

                var type = response.Type.Value;

                switch (type as string)
                {
                    case "Handshake":
                        OnHandshake(client, response.Data);
                        break;
                    case "Alive":
                        OnAlive(client);
                        return;
                    case "Message":
                        OnMessage(client, message);
                        break;
                }
            }
            catch (Exception e)
            {
                client.Error(300, "Could not deserialize message. Not a valid JSON format.");
                Log("Could not deserialize message. Not a valid JSON format: {0}", e.Message);
            }
        }

        #endregion

        #region Handling Client Messages

        /// <summary>
        /// Handles the handshake message. The server will assign an unused glyph if glyph id is not set
        /// by client.
        /// </summary>
        /// <param name="client">The sender of the handshake.</param>
        /// <param name="handshake">Handshake message from the client.</param>
        private void OnHandshake(FleckClient client, dynamic handshake)
        {
            string name = null;
            if (handshake.Name != null)
                name = handshake.Name.Value;

            string glyphId = null;
            if (handshake.GlyphId != null)
                glyphId = handshake.GlyphId.Value;

            string deviceType = null;
            if (handshake.DeviceType != null)
                deviceType = handshake.DeviceType.Value;

            // TODO is this console log necessary???
            if (handshake.Options != null)
                Console.WriteLine(handshake.Options);

            // if glyph id is not set by the client then assign a random id.
            if (glyphId == null || !_deviceIdToGlyph.ContainsKey(glyphId))
            {
                if (_deviceIdQueue.Count > 0)
                {
                    // Get an unsed device id.
                    if (!_deviceIdQueue.TryDequeue(out glyphId))
                        throw new Exception("Could not dequeue device id");
                }
                else
                {
                    // TODO possible improvement of API could be client.Error("ERR300", "Too many connected devices. Try again later."));
                    client.Send("Too many connected devices. Try again later.");
                    return;
                }
            }

            // store client information
            client.Id = glyphId;
            client.Name = name;
            client.DeviceType = deviceType;

            // Log client disconnected message.
            Log("Client {0} [id={1}, deviceType={2}] identified", client.Name, client.Id, client.DeviceType);

            // Get glyph data for device id.
            var glyphData = _deviceIdToGlyph[glyphId];

            // inject the data type
            var serial = string.Format("{{\"Type\":\"{0}\",\"Id\":\"{1}\",\"GlyphData\":\"{2}\"}}", "Glyph", glyphId, glyphData);

            // Send glyph data to device in order to identify device in huddle.
            client.Send(serial);
        }

        /// <summary>
        /// Called each time a client sends an alive message.
        /// </summary>
        /// <param name="sender">The sender of the alive message.</param>
        private void OnAlive(FleckClient sender)
        {
            // do nothing yet
        }

        /// <summary>
        /// Sends the received message to connected clients except for the sender.
        /// </summary>
        /// <param name="sender">The sender of the message, which does not receive its message.</param>
        /// <param name="message">Message sent to connected clients.</param>
        private void OnMessage(FleckClient sender, string message)
        {
            foreach (var c in _connectedClients.Values.Where(c => !c.Equals(sender)))
            {
                c.Send(message);
            }
        }

        #endregion
    }

    /// <summary>
    /// This class wraps around a fleck socket connection and provides high-level methods to communicate
    /// with the client.
    /// </summary>
    public class FleckClient
    {
        #region member fields

        // connection to the client
        private readonly IWebSocketConnection _socket;

        #endregion

        #region properties

        #region Id

        public string Id { get; set; }

        public string Name { get; set; }

        public string DeviceType { get; set; }

        #endregion

        #endregion

        /// <summary>
        /// A fleck client, which provides high-level methods to send data to the client (socket).
        /// </summary>
        /// <param name="socket">Client socket connection</param>
        public FleckClient(IWebSocketConnection socket)
        {
            Id = null;
            Name = null;
            _socket = socket;
        }

        /// <summary>
        /// Sends a message to the client.
        /// </summary>
        /// <param name="message">Message</param>
        public void Send(string message)
        {
            try
            {
                _socket.Send(message);
            }
            catch (Exception e)
            {
                _socket.Send(e.Message);
            }
        }

        /// <summary>
        /// Sends data to the client. The data is serialized with JsonConvert and wrapped into a proper
        /// Huddle message format.
        /// 
        /// {"Type":"[DataType]","Data":[Data]}
        /// </summary>
        /// <param name="data">The data object (it must be serializable with Newtonsoft JsonConvert)</param>
        public void Send(IData data)
        {
            var dataSerial = JsonConvert.SerializeObject(data);

            // inject the data type into the message
            var serial = string.Format(Resources.TemplateDataMessage, data.GetType().Name, dataSerial);

            Send(serial);
        }

        /// <summary>
        /// Sends an error to the client.
        /// </summary>
        /// <param name="code">Error code to decode message on client.</param>
        /// <param name="reason">Reason for the error.</param>
        public void Error(int code, string reason)
        {
            var errorMessage = string.Format(Resources.TemplateErrorMessage, code, reason);

            Send(errorMessage);
        }

        /// <summary>
        /// Sends a bye bye message to the client and closes the connection.
        /// </summary>
        public void Close()
        {
            Send(Resources.TemplateByeByeMessage);
            _socket.Close();
        }

        // override object.Equals
        public override bool Equals(object obj)
        {
            //       
            // See the full list of guidelines at
            //   http://go.microsoft.com/fwlink/?LinkID=85237  
            // and also the guidance for operator== at
            //   http://go.microsoft.com/fwlink/?LinkId=85238
            //

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            var otherClient = obj as FleckClient;
            if (otherClient == null)
                return false;
            
            return Equals(_socket.ConnectionInfo.Id, otherClient._socket.ConnectionInfo.Id);
        }

        // override object.GetHashCode
        public override int GetHashCode()
        {
            return _socket.ConnectionInfo.Id.GetHashCode();
        }
    }
}