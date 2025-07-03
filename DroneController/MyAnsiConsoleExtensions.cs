using Spectre.Console;

namespace DroneController;

public static class MyAnsiConsoleExtensions
{
    // async version of standard AlternateScreen method 
    public static async Task AlternateScreenAsync(this IAnsiConsole console, Func<Task> action)
    {
        if (console is null) throw new ArgumentNullException(nameof(console));

        if (!console.Profile.Capabilities.Ansi)
            throw new NotSupportedException(
                "Alternate buffers are not supported since your terminal does not support ANSI.");

        if (!console.Profile.Capabilities.AlternateBuffer)
            throw new NotSupportedException("Alternate buffers are not supported by your terminal.");

        // Switch to alternate screen
        console.Write(new ControlCode("\u001b[?1049h\u001b[H"));

        try
        {
            // Execute custom action
            await action();
        }
        finally
        {
            // Switch back to primary screen
            console.Write(new ControlCode("\u001b[?1049l"));
        }
    }
}