using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MqttOpcUaBridge
{
    public class OpcUaConsoleClient
    {
        public class OpcItemNotificationEventArgs : EventArgs
        {
            public string DisplayName { get; set; }
            public string Value { get; set; }
            public string NodeId { get; set; }
            public OpcItemNotificationEventArgs()
            {

            }
        }


        public static event EventHandler<OpcItemNotificationEventArgs> NewNotification;

        const int ReconnectPeriod = 10;
        Session session;
        SessionReconnectHandler reconnectHandler;
        string endpointURL;
        int clientRunTime = Timeout.Infinite;
        static bool autoAccept = false;
        static ExitCode exitCode;
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

        private async Task ConsoleSampleClient()
        {
            Console.WriteLine("1 - Create an Application Configuration.");
            exitCode = ExitCode.ErrorCreateApplication;

            ApplicationInstance application = new ApplicationInstance
            {
                ApplicationName = "UA Core Sample Client",
                ApplicationType = ApplicationType.Client,
                ConfigSectionName = Utils.IsRunningOnMono() ? "Opc.Ua.MonoSampleClient" : "Opc.Ua.SampleClient"
            };

            // load the application configuration.
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
            if (endpointURL.Contains("142.250"))
                haveAppCertificate = false;
            var selectedEndpoint = CoreClientUtils.SelectEndpoint(endpointURL, haveAppCertificate, 15000);
            Console.WriteLine("    Selected endpoint uses: {0}",
                selectedEndpoint.SecurityPolicyUri.Substring(selectedEndpoint.SecurityPolicyUri.LastIndexOf('#') + 1));

            Console.WriteLine("3 - Create a session with OPC UA server.");
            exitCode = ExitCode.ErrorCreateSession;
            var endpointConfiguration = EndpointConfiguration.Create(config);
            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, endpointConfiguration);
            session = await Session.Create(config, endpoint, false, "OPC UA Console Client", 60000, new UserIdentity(new AnonymousIdentityToken()), null);

            // register keep alive handler
            session.KeepAlive += Client_KeepAlive;

            Console.WriteLine("4 - Browse the OPC UA server namespace.");
            exitCode = ExitCode.ErrorBrowseNamespace;
            ReferenceDescriptionCollection references;
            Byte[] continuationPoint;

            references = session.FetchReferences(ObjectIds.ObjectsFolder);

            session.Browse(
                null,
                null,
                ObjectIds.ObjectsFolder,
                0u,
                BrowseDirection.Forward,
                ReferenceTypeIds.HierarchicalReferences,
                true,
                (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
                out continuationPoint,
                out references);

            Console.WriteLine(" DisplayName, BrowseName, NodeClass");
            foreach (var rd in references)
            {
                Console.WriteLine(" {0}, {1}, {2}", rd.DisplayName, rd.BrowseName, rd.NodeClass);
                ReferenceDescriptionCollection nextRefs;
                byte[] nextCp;
                session.Browse(
                    null,
                    null,
                    ExpandedNodeId.ToNodeId(rd.NodeId, session.NamespaceUris),
                    0u,
                    BrowseDirection.Forward,
                    ReferenceTypeIds.HierarchicalReferences,
                    true,
                    (uint)NodeClass.Variable | (uint)NodeClass.Object | (uint)NodeClass.Method,
                    out nextCp,
                    out nextRefs);

                foreach (var nextRd in nextRefs)
                {
                    Console.WriteLine("   + {0}, {1}, {2}", nextRd.DisplayName, nextRd.BrowseName, nextRd.NodeClass);
                }
            }

            Console.WriteLine("5 - Create a subscription with publishing interval of 1 second.");
            exitCode = ExitCode.ErrorCreateSubscription;
            var subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = 1000 };
            subscriptions.Add(1000, subscription);
            Console.WriteLine("6 - Add a list of items (server current time and status) to the subscription.");
            exitCode = ExitCode.ErrorMonitoredItem;
            var list = new List<MonitoredItem> {
                new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = "ServerStatusCurrentTime", StartNodeId = "i="+Variables.Server_ServerStatus_CurrentTime.ToString()
                }
            };
            list.ForEach(i => i.Notification += OnNotification);
            subscription.AddItems(list);

            Console.WriteLine("7 - Add the subscription to the session.");
            exitCode = ExitCode.ErrorAddSubscription;
            session.AddSubscription(subscription);
            subscription.Create();

            Console.WriteLine("8 - Running...Press Ctrl-C to exit...");
            exitCode = ExitCode.ErrorRunning;
        }

        internal async Task<uint> Subscribe(string nodeId, int interval)
        {
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
                var list = new List<MonitoredItem> {
                new MonitoredItem(subscription.DefaultItem)
                {
                    DisplayName = nodeId.Substring(nodeId.LastIndexOf("=")), StartNodeId = NodeId.Parse(nodeId)
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

        internal async Task<uint> Write(string nodeId, string payload)
        {
            uint statusCode=await Task.Run(() =>
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

        internal async Task<string> Read(string nodeId)
        {
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

        private static void OnNotification(MonitoredItem item, MonitoredItemNotificationEventArgs e)
        {
            foreach (var value in item.DequeueValues())
            {
                Console.WriteLine("{0}: {1}, {2}, {3}", item.DisplayName, value.Value, value.SourceTimestamp, value.StatusCode);
                if (NewNotification != null)
                    NewNotification(null, new OpcItemNotificationEventArgs()
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