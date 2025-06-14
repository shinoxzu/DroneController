using Asv.Common;
using Asv.IO;
using Asv.Mavlink;
using Asv.Mavlink.Common;
using R3;

namespace DroneController;

public class DroneController: IAsyncDisposable
{
    private readonly IProtocolRouter _router;
    private readonly IProtocolPort _port;
    private readonly IDeviceExplorer _deviceExplorer;
    private readonly IClientDevice _drone;

    private readonly IHeartbeatClient _heartbeatClient;
    private readonly IControlClient _controlClient;
    private readonly IPositionClient _positionClient;

    public ReadOnlyReactiveProperty<GlobalPositionIntPayload?> PositionObserver => _positionClient.GlobalPosition;

    public ReadOnlyReactiveProperty<string?> Name => _drone.Name;

    public DroneController(
        IProtocolRouter router,
        IProtocolPort port,
        IDeviceExplorer deviceExplorer,
        IClientDevice drone,
        IHeartbeatClient heartbeatClient,
        IControlClient controlClient,
        IPositionClient positionClient)
    {
        _router = router;
        _port = port;
        _deviceExplorer = deviceExplorer;
        _drone = drone;
        _heartbeatClient = heartbeatClient;
        _controlClient = controlClient;
        _positionClient = positionClient;
    }

    public async Task StartHeartbeat()
    {
        var tcs = new TaskCompletionSource();

        var count = 0;
        var sub3 = _heartbeatClient.RawHeartbeat
            .ThrottleLast(TimeSpan.FromMilliseconds(100))
            .Subscribe(p =>
            {
                if (p is null)
                {
                    return;
                }

                if (count >= 5)
                {
                    tcs.TrySetResult();
                    return;
                }

                Console.WriteLine($"Heartbeat type: {p.Type}, Heartbeat baseMode: {p.BaseMode}");

                count++;
            });

        await tcs.Task;
    }

    public async Task Land()
    {
        await _controlClient.SetGuidedMode();
        await _controlClient.DoLand();
    }

    public async Task TakeOff(int alt)
    {
        await _controlClient.SetGuidedMode();
        await _controlClient.TakeOff(alt);
    }

    public async Task GoTo(double lat, double lon, double alt)
    {
        await _controlClient.SetGuidedMode();
        await _controlClient.GoTo(new GeoPoint(lat, lon, alt));
    }

    public async ValueTask DisposeAsync()
    {
        await _drone.DisposeAsync();
        await _router.DisposeAsync();
        await _port.DisposeAsync();
        await _deviceExplorer.DisposeAsync();

        await _positionClient.DisposeAsync();
        await _controlClient.DisposeAsync();
        await _heartbeatClient.DisposeAsync();
    }
}