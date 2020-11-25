using MqttBridge.Classes;
using MqttBridge.Interfaces;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Client.Controls;
using Opc.Ua.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MqttBridge
{
    public class OpcUaConsoleClient : IClient
    {
       


        public static event EventHandler<IMonitoredItem> NewNotification;
        public static event EventHandler<IMonitoredItem> NewAlarmNotification;


        const int ReconnectPeriod = 10;
        Session session;
        SessionReconnectHandler reconnectHandler;
        string endpointURL;
        int clientRunTime = Timeout.Infinite;
        static bool autoAccept = false;
        static ExitCode exitCode;
        static Subscription AlarmSubscription;
        static SortedList<int, Subscription> subscriptions;
        public OpcUaConsoleClient(string _endpointURL, bool _autoAccept, int _stopTimeout)
        {
            endpointURL = _endpointURL;
            autoAccept = _autoAccept;
            clientRunTime = _stopTimeout <= 0 ? Timeout.Infinite : _stopTimeout * 1000;
            subscriptions = new SortedList<int, Subscription>();
        }

        public void Run()
        {
            try
            {
                ConsoleSampleClient().Wait();
            }
            catch (Exception ex)
            {
                Utils.Trace("ServiceResultException:" + ex.Message);
                Console.WriteLine("Exception: {0}", ex.Message);
                return;
            }

            ManualResetEvent quitEvent = new ManualResetEvent(false);
            try
            {
                Console.CancelKeyPress += (sender, eArgs) =>
                {
                    quitEvent.Set();
                    eArgs.Cancel = true;
                };
            }
            catch
            {
            }

            // wait for timeout or Ctrl-C
            quitEvent.WaitOne(clientRunTime);

            // return error conditions
            if (session.KeepAliveStopped)
            {
                exitCode = ExitCode.ErrorNoKeepAlive;
                return;
            }

            exitCode = ExitCode.Ok;
        }

        public Task RunAsync()
        {
              return  ConsoleSampleClient();
            
        }

        public static ExitCode ExitCode { get => exitCode; }

        public int SubscribedItemsCount
        {
            get
            {
                int count = 0;
                foreach (var subscription in subscriptions)
                    count += (int)subscription.Value.MonitoredItemCount;
                return count;
            }
        }

        public bool IsConnected
        {
            get
            {
                return session != null && session.Connected;
            }
        }
        private async Task ConsoleSampleClient()
        {
            Console.WriteLine("1 - Create an Application Configuration.");
            exitCode = ExitCode.ErrorCreateApplication;

            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "UA MQTT Bridge Client",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = Utils.IsRunningOnMono() ? "Opc.Ua.MonoSampleClient" : "Opc.Ua.SampleClient"
            };

            // load the application configuration.
            //var config = new ApplicationConfiguration()
            //{
            //    ApplicationName = application.ApplicationName,
            //    ApplicationUri = "urn:localhost:Praewema:MqttBridge",
            //    ProductUri = "https://github.org/andreaskueffel/mqttbridge",
            //    ApplicationType = ApplicationType.Client,
            //    ClientConfiguration = new ClientConfiguration()
            //    {
            //        WellKnownDiscoveryUrls=new StringCollection()
            //        {
            //            "opc.tcp://{0}:4840/UADiscovery"
            //        }
            //    },
            //    SecurityConfiguration = new SecurityConfiguration()
            //    {
            //        ApplicationCertificate = new CertificateIdentifier
            //        {
            //            StoreType = "X509Store",
            //            StorePath = "CurrentUser\\My",
            //            SubjectName = "CN=MqttBridge UA Client, C=DE, S=Hessen, O=Praewema, DC=localhost"
            //        },
            //        TrustedIssuerCertificates = new CertificateTrustList() { StoreType = "Directory", StorePath = "%LocalApplicationData%/OPC Foundation/pki/issuer" },
            //        TrustedPeerCertificates = new CertificateTrustList() { StoreType = "Directory", StorePath = "%LocalApplicationData%/OPC Foundation/pki/trusted" },
            //        RejectedCertificateStore = new CertificateTrustList() { StoreType = "Directory", StorePath = "%LocalApplicationData%/OPC Foundation/pki/rejected" },
            //        AutoAcceptUntrustedCertificates = true,
            //    },
            //    TransportConfigurations=new TransportConfigurationCollection(),
            //    TransportQuotas = new TransportQuotas(),
            //    TraceConfiguration = new TraceConfiguration()
            //    {
            //        OutputFilePath="MqttBridge.Opc.Ua.CoreClient.log",
            //        DeleteOnLoad=true,
            //        TraceMasks=519
            //    },
            //};
            //Utils.Tracing.TraceEventHandler += Tracing_TraceEventHandler;
            ApplicationConfiguration config = await application.LoadApplicationConfiguration(false);
            
            // check the application certificate.
            bool haveAppCertificate = await application.CheckApplicationInstanceCertificate(false, 0);
            if (!haveAppCertificate)
            {
                throw new Exception("Application instance certificate invalid!");
            }

            if (haveAppCertificate)
            {
                config.ApplicationUri = Utils.GetApplicationUriFromCertificate(config.SecurityConfiguration.ApplicationCertificate.Certificate);
                if (config.SecurityConfiguration.AutoAcceptUntrustedCertificates)
                {
                    autoAccept = true;
                }
                config.CertificateValidator.CertificateValidation += new CertificateValidationEventHandler(CertificateValidator_CertificateValidation);
            }
            else
            {
                Console.WriteLine("    WARN: missing application certificate, using unsecure connection.");
            }

            Console.WriteLine("2 - Discover endpoints of {0}.", endpointURL);
            exitCode = ExitCode.ErrorDiscoverEndpoints;
            var identity = new UserIdentity(Program.MqttBridgeSettings.OpcUaUsername,Program.MqttBridgeSettings.OpcUaPassword);
            if (endpointURL.Contains("192.168.142.250")|| String.IsNullOrEmpty(Program.MqttBridgeSettings.OpcUaUsername))
            {
                identity = new UserIdentity(new AnonymousIdentityToken());
                haveAppCertificate = false;
            }
            EndpointDescription selectedEndpoint=null;
            while (selectedEndpoint == null)
            {
                try
                {
                    selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, haveAppCertificate, 2000);
                }
                catch (ServiceResultException) { }
                if (selectedEndpoint == null)
                {
                    Console.Write(".");
                    await Task.Delay(3000);
                }
            }

            Console.WriteLine("    Selected endpoint uses: {0}",
                selectedEndpoint.SecurityPolicyUri.Substring(selectedEndpoint.SecurityPolicyUri.LastIndexOf('#') + 1));

            Console.WriteLine("3 - Create a session with OPC UA server.");
            exitCode = ExitCode.ErrorCreateSession;
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            session = await Session.Create(config, endpoint, false, "OPC UA Console Client", 60000, identity, null);

            // register keep alive handler
            session.KeepAlive += Client_KeepAlive;

            //Console.WriteLine("4 - Browse the OPC UA server namespace.");
            //exitCode = ExitCode.ErrorBrowseNamespace;
            //ReferenceDescriptionCollection references;
            //Byte[] continuationPoint;

            //references = session.FetchReferences(ObjectIds.ObjectsFolder);

            //session.Browse(
            //    null,
            //    null,
            //    ObjectIds.ObjectsFolder,
            //    0u,
            //    BrowseDirection.Forward,
            //    ReferenceTypeIds.HierarchicalReferences,
            //    true,
            //    (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
            //    out continuationPoint,
            //    out references);

            //Console.WriteLine(" DisplayName, BrowseName, NodeClass");
            //foreach (var rd in references)
            //{
            //    Console.WriteLine(" {0}, {1}, {2}", rd.DisplayName, rd.BrowseName, rd.NodeClass);
            //    ReferenceDescriptionCollection nextRefs;
            //    byte[] nextCp;
            //    session.Browse(
            //        null,
            //        null,
            //        ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris),
            //        0u,
            //        BrowseDirection.Forward,
            //        ReferenceTypeIds.HierarchicalReferences,
            //        true,
            //        (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
            //        out nextCp,
            //        out nextRefs);

            //    foreach (var nextRd in nextRefs)
            //    {
            //        Console.WriteLine("   + {0}, {1}, {2}", nextRd.DisplayName, nextRd.BrowseName, nextRd.NodeClass);
            //    }
            //}

            //Console.WriteLine("5 - Create a subscription with publishing interval of 1 second.");
            //exitCode = ExitCode.ErrorCreateSubscription;
            //var subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = 1000 };
            //subscriptions.Add(1000, subscription);
            //Console.WriteLine("6 - Add a list of items (server current time and status) to the subscription.");
            //exitCode = ExitCode.ErrorMonitoredItem;
            //var list = new List<Opc.Ua.Client.MonitoredItem> {
            //    new Opc.Ua.Client.MonitoredItem(subscription.DefaultItem)
            //    {
            //        DisplayName = "ServerStatusCurrentTime", StartNodeId = "i="+ Variables.Server_ServerStatus_CurrentTime.ToString()
            //    }
            //};
            //list.ForEach(i => i.Notification += OnNotification);
            //subscription.AddItems(list);

            //Console.WriteLine("7 - Add the subscription to the session.");
            //exitCode = ExitCode.ErrorAddSubscription;
            //session.AddSubscription(subscription);
            //subscription.Create();

            Console.WriteLine("8 - Running...");

            Console.WriteLine("9 - Alarms/Events...TODO IMPLEMENT!!");
            AlarmSubscription = new Subscription(session.DefaultSubscription) { PublishingInterval = 1000, MaxNotificationsPerPublish=1000, PublishingEnabled=true };
            session.AddSubscription(AlarmSubscription);
            AlarmSubscription.Create();
            ExpandedNodeId nodes = null;
            session.FetchTypeTree(nodes);
            FilterDeclaration filterDeclaration = new FilterDeclaration();
            FilterDeclaration.UpdateFilter(filterDeclaration, new NodeId(2041));
            EventFilter eventFilter = new EventFilter();
            eventFilter.AddSelectClause(new NodeId(2041), "BaseEventType", Attributes.Value);
            //eventFilter.WhereClause.Elements.Add(new ContentFilterElement() { });
            var AlarmItem = new Opc.Ua.Client.MonitoredItem(AlarmSubscription.DefaultItem)
            {
                DisplayName = "DiagnosisLogbook"/*.Substring(nodeId.LastIndexOf("=")*/,
                StartNodeId = NodeId.Parse("ns=16;s=DiagnosisLogbook"),
                AttributeId = Attributes.EventNotifier,
                SamplingInterval = 0,
                QueueSize = 1000,
                Filter = eventFilter,
                DiscardOldest =true
            };
            AlarmItem.Notification += (item, eventargs) => {
                Trace.WriteLine(item.DisplayName + " - " + eventargs.NotificationValue.ToString());
            };
            AlarmSubscription.AddItem(AlarmItem);

            AlarmSubscription.ApplyChanges();
            Console.WriteLine("10 - Done.");

            exitCode = ExitCode.ErrorRunning;
        }

        private void Tracing_TraceEventHandler(object sender, TraceEventArgs e)
        {
            System.Diagnostics.Trace.WriteLine(String.Format(e.Format,e.Arguments));
        }

        public async Task<uint> Subscribe(string rawNodeId, int interval)
        {
            string nodeId = ReformatNodeId(rawNodeId);
            uint statuscode = await Task.Run(() =>
            {

                if (!subscriptions.ContainsKey(interval))
                {
                    var subs = new Subscription(session.DefaultSubscription) { PublishingInterval = interval };
                    subscriptions.Add(interval, subs);
                }
                var subscription = subscriptions[interval];

                //Wenn das Item schon subscribed ist wieder zurück
                foreach (var i in subscription.MonitoredItems)
                    if (i.StartNodeId == NodeId.Parse(nodeId))
                        return (uint)0;

                //Sonst hinzufügen
                var list = new List<Opc.Ua.Client.MonitoredItem> {
                new Opc.Ua.Client.MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = rawNodeId/*.Substring(nodeId.LastIndexOf("=")*/, StartNodeId = NodeId.Parse(nodeId), SamplingInterval=interval
                }
            };
                list.ForEach(i => i.Notification += OnNotification);
                subscription.AddItems(list);

                bool sessionContainsSubscription = false;
                foreach (var s in session.Subscriptions)
                    if (s == subscription)
                        sessionContainsSubscription = true;

                if (!sessionContainsSubscription)
                    session.AddSubscription(subscription);

                if (!subscription.Created)
                    subscription.Create();
                
                subscription.ApplyChanges();
                return (uint)0;
            });
            return statuscode;
        }

        public async Task<uint> Unsubscribe(string rawNodeId)
        {
            string nodeId = ReformatNodeId(rawNodeId);
            uint statuscode = await Task.Run(() =>
            {
                //Ungetestet !!
                foreach(var subscription in subscriptions)
                {
                    foreach(var item in subscription.Value.MonitoredItems)
                    {
                        if (item.StartNodeId == NodeId.Parse(nodeId))
                        {
                            try { item.Notification -= OnNotification; } catch { }
                            subscription.Value.RemoveItem(item);
                        }
                    }                        
                subscription.Value.ApplyChanges();
                }
               
                return (uint)0;
            });
            return statuscode;
        }


        public async Task<uint> Write(string nodeId, string payload)
        {
            nodeId = ReformatNodeId(nodeId);
            uint statusCode =await Task.Run(() =>
            {
                DataValueCollection dataValues = new DataValueCollection();
                DiagnosticInfoCollection diagnosticInfosRead = new DiagnosticInfoCollection();
                var r = new ReadValueId() { NodeId = NodeId.Parse(nodeId), AttributeId = Attributes.Value };
                session.Read(null, 0, TimestampsToReturn.Neither, new ReadValueIdCollection() { r }, out dataValues, out diagnosticInfosRead);

                //Wenn die NodeId nicht gelesen werden konnte zurück
                if (!StatusCode.IsGood(dataValues[0].StatusCode))
                    return dataValues[0].StatusCode.Code;
            
                var x = dataValues[0].WrappedValue.TypeInfo;
                WriteValue w = new WriteValue();
                var y = TypeInfo.Cast(payload, x.BuiltInType);
                w.NodeId = NodeId.Parse(nodeId);
                w.Value = new DataValue(new Variant(y));
                w.AttributeId = Attributes.Value;
                StatusCodeCollection statusCodes = new StatusCodeCollection();
                DiagnosticInfoCollection diagnosticInfos = new DiagnosticInfoCollection();
                session.Write(null, new WriteValueCollection() { w }, out statusCodes, out diagnosticInfos);
                return statusCodes[0].Code;
            });
            return statusCode;
        }

        public async Task<string> Read(string nodeId)
        {
            nodeId = ReformatNodeId(nodeId);
            string ValueAsString = await Task.Run(() =>
            {
                DataValueCollection dataValues = new DataValueCollection();
                DiagnosticInfoCollection diagnosticInfosRead = new DiagnosticInfoCollection();
                var r = new ReadValueId() { NodeId = NodeId.Parse(nodeId), AttributeId = Attributes.Value };
                session.Read(null, 0, TimestampsToReturn.Neither, new ReadValueIdCollection() { r }, out dataValues, out diagnosticInfosRead);

                //Wenn die NodeId nicht gelesen werden konnte zurück
                if (StatusCode.IsGood(dataValues[0].StatusCode))
                    return dataValues[0].Value.ToString();
                else
                    return null;

            });
            return ValueAsString;
        }

        private void Client_KeepAlive(Session sender, KeepAliveEventArgs e)
        {
            if (e.Status != null && ServiceResult.IsNotGood(e.Status))
            {
                Console.WriteLine("{0} {1}/{2}", e.Status, sender.OutstandingRequestCount, sender.DefunctRequestCount);

                if (reconnectHandler == null)
                {
                    Console.WriteLine("--- RECONNECTING ---");
                    reconnectHandler = new SessionReconnectHandler();
                    reconnectHandler.BeginReconnect(sender, ReconnectPeriod * 1000, Client_ReconnectComplete);
                }
            }
        }

        string ReformatNodeId(string nodeId)
        {
            if (nodeId.StartsWith("channel/parameter/r"))
                nodeId = "ns=2;s=" + nodeId.Replace("channel/parameter/r", "/Channel/Parameter/R");
            if (!nodeId.StartsWith("ns=2") && !nodeId.StartsWith("i="))
                nodeId = "ns=2;s=/" + nodeId;
            return nodeId;
        }

        private void Client_ReconnectComplete(object sender, EventArgs e)
        {
            // ignore callbacks from discarded objects.
            if (!Object.ReferenceEquals(sender, reconnectHandler))
            {
                return;
            }

            session = reconnectHandler.Session;
            reconnectHandler.Dispose();
            reconnectHandler = null;

            Console.WriteLine("--- RECONNECTED ---");
        }

        private static void OnNotification(Opc.Ua.Client.MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            foreach (var value in item.DequeueValues())
            {
                //Console.WriteLine("{0}: {1}, {2}, {3}", item.DisplayName, value.Value, value.SourceTimestamp, value.StatusCode);
                if (NewNotification != null)
                    NewNotification(null, new MonitoredItemOpcUa()
                    {
                        NodeId = item.StartNodeId.ToString(),
                        DisplayName = item.DisplayName,
                        Value = value.Value.ToString()
                    });
            }
        }

        private static void CertificateValidator_CertificateValidation(CertificateValidator validator, CertificateValidationEventArgs e)
        {
            if (e.Error.StatusCode == StatusCodes.BadCertificateUntrusted)
            {
                e.Accept = autoAccept;
                if (autoAccept)
                {
                    Console.WriteLine("Accepted Certificate: {0}", e.Certificate.Subject);
                }
                else
                {
                    Console.WriteLine("Rejected Certificate: {0}", e.Certificate.Subject);
                }
            }
        }

    }


    public enum ExitCode : int
    {
        Ok = 0,
        ErrorCreateApplication = 0x11,
        ErrorDiscoverEndpoints = 0x12,
        ErrorCreateSession = 0x13,
        ErrorBrowseNamespace = 0x14,
        ErrorCreateSubscription = 0x15,
        ErrorMonitoredItem = 0x16,
        ErrorAddSubscription = 0x17,
        ErrorRunning = 0x18,
        ErrorNoKeepAlive = 0x30,
        ErrorInvalidCommandLine = 0x100
    }

}