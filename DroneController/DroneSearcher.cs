using Asv.Cfg;
using Asv.IO;
using Asv.Mavlink;
using ObservableCollections;
using R3;

namespace DroneController;

public record SearcherConfig(string RouterHost, int RouterPort, string RouterId);

public class DroneSearcher: IDisposable, IAsyncDisposable
{
    private const int InitilizationLimitMs = 10 * 1000;
    private const int SystemId = 255;
    private const int ComponentId = 255;
    private const int DeviceBrowserTimeoutMs = 1 * 1000;
    private const int DeviceBrowserCheckIntervalMs = 30 * 1000;
    private const int SearchDeviceTimeoutSecs = 5;

    private readonly SearcherConfig _config;
    private readonly IProtocolRouter _router;
    private readonly IProtocolPort _port;
    private readonly IDeviceExplorer _deviceExplorer;
    
    public DroneSearcher(SearcherConfig config)
    {
        _config = config;
        
        _router = CreateRouter();
        _port = CreatePort(_router);
        _deviceExplorer = CreateDeviceExplorer(_router);
    }

    public async Task<Drone?> SearchDrone()
    {
        var device = await SearchDevice(_deviceExplorer);
        if (device is null) return null;

        await device.WaitUntilConnectAndInit(InitilizationLimitMs, TimeProvider.System);
        
        return new Drone(device);
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

    private IProtocolRouter CreateRouter()
    {
        var protocol = Protocol.Create(builder =>
        {
            builder.RegisterMavlinkV2Protocol();
            builder.Features.RegisterBroadcastFeature<MavlinkMessage>();
            builder.Formatters.RegisterSimpleFormatter();
        });
        return protocol.CreateRouter(_config.RouterId);
    }

    private IProtocolPort CreatePort(IProtocolRouter router)
    {
        return router.AddTcpClientPort(p =>
        {
            p.Host = _config.RouterHost;
            p.Port = _config.RouterPort;
        });
    }

    public void Dispose()
    {
        _router.Dispose();
        _deviceExplorer.Dispose();
        _port.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _router.DisposeAsync();
        await _deviceExplorer.DisposeAsync();
        await _port.DisposeAsync();
    }
}