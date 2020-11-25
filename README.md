# Ctrl2MqttBridge

Provides connectivity to an industrial controller via MQTT
Used in production environment to connect Siemens 840dsl
and Rexroth MTX (CML85) controllers to HMI software.


## Main features

- Read/Write values
- Subscribe to items
- Unsubscribe from items

## Included subprojects

- CtrlConnection -> use this dll in any project to easily connect to any controller
- SetupCreator/SetupTools -> used to build the MqttBridgeSetup.exe sfx installer
- Siemens.Sinumerik.Operate.Services.Stub -> Stub for the commercial Siemens OperateNet Libraries

## Prerequisites

- Connectivity can base on OPC UA 
- or via OperateNET library (provided by Siemens)
- To use OperateNET library the "Siemens.Sinumerik.Operate.Services.dll" files are required in External directory during build

## Usage of Ctrl2MqttBridge / CtrlConnection

### Ctrl2MqttBridge

- Bridge is either installed via the SFX installer or the -i switch
- It integrates in Sinumerik Operate PROC610 and runs in the background
- Check configuration to setup
 - OpcUaMode -> TRUE uses OPC UA, FALSE will try to use OperateNet and falls back to OPC UA when it fails
 - ServerName -> IP of OPC UA Server to connect to
 - MqttPort -> stays 51883 unless there is a need to change. Must corelate to CtrlConnection
 - OpcUaPort -> Port of OPC UA Server to connect to
 - OpcUaUsername -> Username for OPC UA Server
 - OpcUaPassword -> Password for OPC UA Server
 - ExternalBrokerUrl -> attach all topics to an external broker (for debugging)
 - EnableExternalBroker -> false unless there is a need to use it
 - EnableStatus -> true to see BridgeStatus messages

### CtrlConnection

- Just put a reference on the CtrlConnection library (via NuGet)
- setup a new class and inherit from BridgeConnection
``` cs
public class CtrlConnection : BridgeConnection
{

    public CtrlConnection()
    {
        ConnectionHandler += CtrlConnection_ConnectionHandler;
        ConnectSync("192.168.214.241",51883,new Guid().ToString());
    }


    private void CtrlConnection_ConnectionHandler(object sender, bool e)
    {
        if (e)
            SubsClientThread();
    }

    void SubsClientThread()
    {
        if (subsc_Main == null)
        {
            subsc_Main = new SubscriptionHelper();
            subsc_Main.DataChanged += Subsc_Main_DataChanged;
        }
        AddMonitoredOPCItem("/Channel/Parameter/r[u1,1]", subsc_Main);
    }

    private void Subsc_Main_DataChanged(object sender, MonitoredItem monitoredItem)
    {
        //Callback when R1 changed...
    }
}

```
 - ToDo: improve documentation
