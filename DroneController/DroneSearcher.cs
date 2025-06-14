using Asv.Cfg;
using Asv.IO;
using Asv.Mavlink;
using ObservableCollections;
using R3;

namespace DroneController;

public record SearcherConfig(string RouterHost, int RouterPort, string RouterId);

public class DroneSearcher
{
    private const int InitilizationLimitMs = 10 * 1000;
    private const int SystemId = 255;
    private const int ComponentId = 255;
    private const int DeviceBrowserTimeoutMs = 1 * 1000;
    private const int DeviceBrowserCheckIntervalMs = 30 * 1000;
    private const int SearchDeviceTimeoutSecs = 5;

    public async Task<Drone?> SearchDrone(SearcherConfig config)
    {
        var router = CreateRouter(config);
        var port = CreatePort(router, config);

        var deviceExplorer = CreateDeviceExplorer(router);
        var device = await SearchDevice(deviceExplorer);

        if (device is null) return null;

        await device.WaitUntilConnectAndInit(InitilizationLimitMs, TimeProvider.System);

        var heartbeatClient = device.GetMicroservice<IHeartbeatClient>();
        if (heartbeatClient is null) throw new Exception("No heartbeat client found; cannot use this device");

        var controlClient = device.GetMicroservice<IControlClient>();
        if (controlClient is null) throw new Exception("No control client found; cannot use this device");

        var positionClient = device.GetMicroservice<IPositionClient>();
        if (positionClient is null) throw new Exception("No position client found; cannot use this device");

        return new Drone(router, port, deviceExplorer, device, heartbeatClient, controlClient,
            positionClient);
    }

    private IDeviceExplorer CreateDeviceExplorer(IProtocolRouter router)
    {
        var seq = new PacketSequenceCalculator();
        var identity = new MavlinkIdentity(SystemId, ComponentId);

        return DeviceExplorer.Create(router, builder =>
        {
            builder.SetConfig(new ClientDeviceBrowserConfig
            {
                DeviceTimeoutMs = DeviceBrowserTimeoutMs,
                DeviceCheckIntervalMs = DeviceBrowserCheckIntervalMs
            });
            builder.Factories.RegisterDefaultDevices(
                new MavlinkIdentity(identity.SystemId, identity.ComponentId),
                seq,
                new InMemoryConfiguration());
        });
    }

    private async Task<IClientDevice?> SearchDevice(IDeviceExplorer explorer)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(SearchDeviceTimeoutSecs), TimeProvider.System);

        try
        {
            return await explorer.Devices
                .ObserveAdd()
                .Select(kvp => kvp.Value.Value)
                .FirstAsync(cts.Token);
        }
        catch (TaskCanceledException)
        {
            return null;
        }
    }

    private IProtocolRouter CreateRouter(SearcherConfig config)
    {
        var protocol = Protocol.Create(builder =>
        {
            builder.RegisterMavlinkV2Protocol();
            builder.Features.RegisterBroadcastFeature<MavlinkMessage>();
            builder.Formatters.RegisterSimpleFormatter();
        });
        return protocol.CreateRouter(config.RouterId);
    }

    private IProtocolPort CreatePort(IProtocolRouter router, SearcherConfig config)
    {
        return router.AddTcpClientPort(p =>
        {
            p.Host = config.RouterHost;
            p.Port = config.RouterPort;
        });
    }
}