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

            await using var searcher = new DroneSearcher(searcherConfig);
            await using var drone = await AnsiConsole.Status()
                .StartAsync("Searching the device...", async _ => await searcher.SearchDrone());

            if (drone is null)
            {
                AnsiConsole.MarkupLine("[red]Cannot find the device :(\nTry to use a different connection params.[/]");
                return 0;
            }

            var layout = CreateBasicLayout();
            var cancellationToken = RegisterOnGracefulShutdown();

            while (!cancellationToken.IsCancellationRequested)
            {
                AnsiConsole.Clear();

                try
                {
                    var key = await WaitForControlKeyAndRenderAsync(layout, drone, cancellationToken.Token);
                    if (key is null or ConsoleKey.E) break;

                    switch (key.Value)
                    {
                        case ConsoleKey.T:
                        {
                            await AnsiConsole.Console.AlternateScreenAsync(async () =>
                            {
                                var alt = await AnsiConsole.PromptAsync(
                                    new TextPrompt<int>("Alt (in meters):"), cancellationToken.Token);
                                await drone.TakeOff(alt);
                            });
                            break;
                        }
                        case ConsoleKey.L:
                        {
                            await drone.Land();
                            break;
                        }
                        case ConsoleKey.G:
                        {
                            await AnsiConsole.Console.AlternateScreenAsync(async () =>
                            {
                                var lat = await AnsiConsole.PromptAsync(
                                    new TextPrompt<double>("Latitude:"), cancellationToken.Token);
                                var lon = await AnsiConsole.PromptAsync(
                                    new TextPrompt<double>("Longitude:"), cancellationToken.Token);
                                var alt = await AnsiConsole.PromptAsync(
                                    new TextPrompt<double>("Altitude (in meters):"), cancellationToken.Token);

                                await drone.GoTo(new GeoPoint(lat, lon, alt));
                            });

                            break;
                        }
                        case ConsoleKey.E:
                        {
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    await AnsiConsole.Console.AlternateScreenAsync(async () =>
                    {
                        AnsiConsole.MarkupLineInterpolated($"[red]Error:[/] {ex.Message}");
                        AnsiConsole.MarkupLine("[white]Hint: Press any key to continue...[/]");
                        await AnsiConsole.Console.Input.ReadKeyAsync(true, cancellationToken.Token);
                    });
                }
            }

            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[white]Bye![/]");

            return 0;
        }

        private async Task<ConsoleKey?> WaitForControlKeyAndRenderAsync(
            Layout layout,
            Drone drone,
            CancellationToken cancelToken)
        {
            ConsoleKey? result = null;

            await AnsiConsole.Live(layout)
                .AutoClear(true)
                .StartAsync(async ctx =>
                {
                    using var nameSubscription = drone.Name.Subscribe(v =>
                    {
                        if (v is null) return;

                        layout["Status"]["Name"].Update(new Panel(
                                Align.Center(
                                    Markup.FromInterpolated(
                                        $"Named of the used drone: [#9cd6ff]{v}[/]"),
                                    VerticalAlignment.Middle))
                            .Expand());
                    });

                    using var positionSubscription = drone.PositionObserver.Subscribe(s =>
                    {
                        if (s is null) return;

                        layout["Status"]["Position"].Update(new Panel(
                                Align.Center(
                                    new Markup(
                                        $"[#9cd6ff]Lat[/]: {s.Lat / 10_000_000f} | " +
                                        $"[#9cd6ff]Lon[/]: {s.Lon / 10_000_000f} | " +
                                        $"[#9cd6ff]Alt[/]: {s.Alt / 1_000f}m"),
                                    VerticalAlignment.Middle))
                            .Expand());
                    });

                    while (!cancelToken.IsCancellationRequested)
                    {
                        // we have to refresh context only in one ("main") thread per time
                        ctx.Refresh();

                        if (!AnsiConsole.Console.Input.IsKeyAvailable())
                        {
                            await Task.Delay(100, cancelToken);
                            continue;
                        }

                        var keyInfo = await AnsiConsole.Console
                            .Input
                            .ReadKeyAsync(true, cancelToken);
                        if (keyInfo is null)
                            continue;

                        switch (keyInfo.Value.Key)
                        {
                            case ConsoleKey.T:
                            case ConsoleKey.L:
                            case ConsoleKey.G:
                            case ConsoleKey.E:
                                result = keyInfo.Value.Key;
                                return;
                        }
                    }
                });

            return result;
        }

        private CancellationTokenSource RegisterOnGracefulShutdown()
        {
            var cancellationToken = new CancellationTokenSource();

            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellationToken.Cancel();
            };

            return cancellationToken;
        }

        private Layout CreateBasicLayout()
        {
            var layout = new Layout("Root")
                .SplitColumns(
                    new Layout("Main"),
                    new Layout("Status")
                        .SplitRows(
                            new Layout("Name"),
                            new Layout("Position")));

            var commandsPanel = new Panel(new Rows(
                new Markup("[white]t[/] takeoff"),
                new Markup("[white]l[/] land"),
                new Markup("[white]g[/] go"),
                new Markup("[white]e[/] exit")
            ));
            commandsPanel.Header = new PanelHeader("Actions");
            commandsPanel.HeaderAlignment(Justify.Center);
            commandsPanel.Border = BoxBorder.Rounded;
            commandsPanel.Padding = new Padding(2, 1);

            layout["Main"].Update(new Panel(Align.Center(commandsPanel, VerticalAlignment.Middle)).Expand());

            return layout;
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