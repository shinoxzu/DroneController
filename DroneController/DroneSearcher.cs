using Asv.Cfg;
using Asv.IO;
using Asv.Mavlink;
using ObservableCollections;
using R3;

namespace DroneController;

public record SearcherConfig(string RouterHost, int RouterPort, string RouterId);

public class DroneSearcher
{
    private const int InitilizationLimitMs =  10 * 1000;
    private const int SystemId = 255;
    private const int ComponentId = 255;
    private const int DeviceBrowserTimeoutMs = 1 * 1000;
    private const int DeviceBrowserCheckIntervalMs = 30 * 1000;
    private const int SearchDeviceTimeout = 60 * 1000;

    public async Task<DroneController?> SearchDrone(SearcherConfig config)
    {
        var router = CreateRouter(config);
        var port = CreatePort(router, config);

        var deviceExplorer = CreateDeviceExplorer(router);
        var device = await SearchDevice(deviceExplorer);
        await device.WaitUntilConnectAndInit(InitilizationLimitMs, TimeProvider.System);

        var heartbeatClient = device.GetMicroservice<IHeartbeatClient>();
        if (heartbeatClient is null) throw new Exception("No heartbeat client found");

        var controlClient = device.GetMicroservice<IControlClient>();
        if (controlClient is null) throw new Exception("No control client found");

        var positionClient = device.GetMicroservice<IPositionClient>();
        if (positionClient is null) throw new Exception("No position client found");

        return new DroneController(router, port, deviceExplorer, device, heartbeatClient, controlClient, positionClient);
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
                DeviceCheckIntervalMs = DeviceBrowserCheckIntervalMs,
            });
            builder.Factories.RegisterDefaultDevices(
                new MavlinkIdentity(identity.SystemId, identity.ComponentId),
                seq,
                new InMemoryConfiguration());
        });
    }

    private async Task<IClientDevice> SearchDevice(IDeviceExplorer explorer)
    {
        var tcs = new TaskCompletionSource();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(SearchDeviceTimeout), TimeProvider.System);
        await using var s = cts.Token.Register(() => tcs.TrySetCanceled());

        IClientDevice? drone = null;
        using var sub = explorer.Devices
            .ObserveAdd()
            .Take(1)
            .Subscribe(kvp =>
            {
                drone = kvp.Value.Value;
                tcs.TrySetResult();
            });

        await tcs.Task;

        if (drone is null)
        {
            throw new Exception("Drone not found");
        }

        return drone;
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