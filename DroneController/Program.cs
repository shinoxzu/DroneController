using System.ComponentModel;
using DroneController;
using Spectre.Console;
using Spectre.Console.Cli;
using R3;

var app = new CommandApp<DroneControllerCommand>();
return app.Run(args);

internal sealed class DroneControllerCommand : AsyncCommand<DroneControllerCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Router id.")]
        [CommandOption("-i")]
        [DefaultValue("ROUTER")]
        public required string RouterId { get; init; }

        [Description("Router port.")]
        [CommandOption("-p")]
        [DefaultValue(5760)]
        public required int RouterPort { get; init; }

        [Description("Router host.")]
        [CommandOption("-h")]
        [DefaultValue("127.0.0.1")]
        public required string RouterHost { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var searcherConfig = new SearcherConfig(settings.RouterHost, settings.RouterPort, settings.RouterId);
        var searcher = new DroneSearcher();

        await using var drone = await searcher.SearchDrone(searcherConfig);
        if (drone is null)
        {
            AnsiConsole.MarkupLine("[red]Cannot find a device :(\nTry to use a different router host and port.[/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[green]Drone found![/]");

        using var nameSubscription = drone.Name.Subscribe(v =>
        {
            AnsiConsole.MarkupLineInterpolated($"Named of the used drone: [#9cd6ff]{drone.Name.CurrentValue}[/]");
        });

        while (true)
        {
            var command = await AnsiConsole.PromptAsync(
                new SelectionPrompt<string>()
                    .Title("Available commands:")
                    .PageSize(10)
                    .HighlightStyle(new Style().Foreground(Color.FromHex("#9cd6ff")))
                    .AddChoices("takeoff", "land", "go", "status", "exit", "help"));

            try
            {
                switch (command)
                {
                    case "takeoff":
                        var altTakeoff = AnsiConsole.Prompt(new TextPrompt<int>("Alt (in meters)?"));
                        await drone.TakeOff(altTakeoff);
                        AnsiConsole.WriteLine("Success!");
                        break;
                    case "land":
                        await drone.Land();
                        AnsiConsole.WriteLine("Success!");
                        break;
                    case "go":
                        var lat = AnsiConsole.Prompt(new TextPrompt<double>("Lat?"));
                        var lon = AnsiConsole.Prompt(new TextPrompt<double>("Lon?"));
                        var altGo = AnsiConsole.Prompt(new TextPrompt<double>("Alt (in meters)?"));

                        await drone.GoTo(lat, lon, altGo);

                        AnsiConsole.WriteLine("Success!");
                        break;
                    case "status":
                        await ShowLiveStatusAsync(drone);
                        break;
                    case "exit":
                        AnsiConsole.WriteLine("Bye!");
                        return 0;
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteLine($"Error: {ex}");
                AnsiConsole.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
        }
    }

    private async Task ShowLiveStatusAsync(DroneController.DroneController drone)
    {
        using var cts = new CancellationTokenSource();

        AnsiConsole.MarkupLine("[white]Hint: Press q to return to the menu[/]");

        using var sub = drone.PositionObserver.Subscribe(s =>
        {
            if (s is null) return;
            AnsiConsole.Markup("\r" +
                $"[#9cd6ff]Lat[/]: {s.Lat/10_000_000f} | " +
                $"[#9cd6ff]Lon[/]: {s.Lon/10_000_000f} | " +
                $"[#9cd6ff]Alt[/]: {s.Alt/1_000f}m");
        });

        while (!cts.Token.IsCancellationRequested)
        {
            if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Q)
            {
                AnsiConsole.WriteLine();
                break;
            }

            await Task.Delay(100, cts.Token);
        }
    }
}