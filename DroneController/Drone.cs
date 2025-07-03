using Asv.Common;
using Asv.IO;
using Asv.Mavlink;
using Asv.Mavlink.Common;
using R3;

namespace DroneController;

public class Drone : IAsyncDisposable, IDisposable
{
    private readonly IControlClient _controlClient;
    private readonly IClientDevice _device;

    private readonly IHeartbeatClient _heartbeatClient;
    private readonly IPositionClient _positionClient;

    public Drone(IClientDevice device)
    {
        _device = device;
        
        _heartbeatClient = device.GetMicroservice<IHeartbeatClient>() ??
                           throw new Exception("No heartbeat client found; cannot use this device");
        _controlClient = device.GetMicroservice<IControlClient>() ??
                         throw new Exception("No control client found; cannot use this device");
        _positionClient = device.GetMicroservice<IPositionClient>() ??
                          throw new Exception("No position client found; cannot use this device");
    }

    public ReadOnlyReactiveProperty<GlobalPositionIntPayload?> PositionObserver => _positionClient.GlobalPosition;

    public ReadOnlyReactiveProperty<string?> Name => _device.Name;

    public async ValueTask DisposeAsync()
    {
        await _device.DisposeAsync();
        
        await _positionClient.DisposeAsync();
        await _controlClient.DisposeAsync();
        await _heartbeatClient.DisposeAsync();
    }

    public void Dispose()
    {
        _device.Dispose();

        _positionClient.Dispose();
        _controlClient.Dispose();
        _heartbeatClient.Dispose();
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

    public async Task GoTo(GeoPoint point)
    {
        await _controlClient.SetGuidedMode();
        await _controlClient.GoTo(point);
    }
}