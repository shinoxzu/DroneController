using System.ComponentModel;
using Asv.Common;
using DroneController;
using R3;
using Spectre.Console;
using Spectre.Console.Cli;

var app = new CommandApp<DroneControllerCommand>();
return app.Run(args);

namespace DroneController
{
    internal sealed class DroneControllerCommand : AsyncCommand<DroneControllerCommand.Settings>
    {
        public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
        {
            var searcherConfig = new SearcherConfig(settings.RouterHost, settings.RouterPort, settings.RouterId);
            var searcher = new DroneSearcher();

            await using var drone = await AnsiConsole.Status()
                .StartAsync("Searching the device...", async _ => await searcher.SearchDrone(searcherConfig));

            if (drone is null)
            {
                AnsiConsole.MarkupLine(
                    "[red]Cannot find the device :(\nTry to use a different router host and port.[/]");
                return 0;
            }

            using var nameSubscription = drone.Name.Subscribe(v =>
            {
                AnsiConsole.MarkupLineInterpolated(
                    $"Named of the used drone: [#9cd6ff]{drone.Name.CurrentValue}[/]");
            });

            while (true)
            {
                var command = await AnsiConsole.PromptAsync(
                    new SelectionPrompt<string>()
                        .Title("Available commands:")
                        .PageSize(10)
                        .HighlightStyle(new Style().Foreground(Color.FromHex("#9cd6ff")))
                        .AddChoices("takeoff", "land", "go", "status", "exit"));

                try
                {
                    switch (command)
                    {
                        case "takeoff":
                            var altTakeoff = AnsiConsole.Prompt(new TextPrompt<int>("Alt (in meters)?"));
                            await drone.TakeOff(altTakeoff);
                            AnsiConsole.MarkupLine("[green]Takeoff successfully![/]");
                            break;
                        case "land":
                            await drone.Land();
                            AnsiConsole.MarkupLine("[green]Land successfully![/]");
                            break;
                        case "go":
                            var lat = AnsiConsole.Prompt(new TextPrompt<double>("Latitude:"));
                            var lon = AnsiConsole.Prompt(new TextPrompt<double>("Longitude:"));
                            var altGo = AnsiConsole.Prompt(new TextPrompt<double>("Altitude (in meters):"));

                            await drone.GoTo(new GeoPoint(lat, lon, altGo));

                            AnsiConsole.MarkupLine("[green]Drone is moving to the provided position![/]");
                            break;
                        case "status":
                            ShowLiveStatus(drone);
                            break;
                        case "exit":
                            AnsiConsole.MarkupLine("[white]Bye![/]");
                            return 0;
                    }
                }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
                    AnsiConsole.MarkupLine("[white]Hint: Press any key to continue...[/]");
                    Console.ReadKey();
                }
            }
        }

        private void ShowLiveStatus(Drone drone)
        {
            AnsiConsole.Console.AlternateScreen(() =>
            {
                AnsiConsole.MarkupLine("[white]Hint: Press Esc to return to the menu[/]\n");

                using var sub = drone.PositionObserver.Subscribe(s =>
                {
                    if (s is null) return;
                    AnsiConsole.Markup("\r" +
                                       $"[#9cd6ff]Lat[/]: {s.Lat / 10_000_000f} | " +
                                       $"[#9cd6ff]Lon[/]: {s.Lon / 10_000_000f} | " +
                                       $"[#9cd6ff]Alt[/]: {s.Alt / 1_000f}m");
                });

                while (true)
                {
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                    {
                        AnsiConsole.WriteLine();
                        break;
                    }

                    Thread.Sleep(100);
                }
            });
        }

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
    }
}