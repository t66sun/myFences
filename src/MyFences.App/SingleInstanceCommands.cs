using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Security.Principal;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MyFences.App;

internal sealed record InstanceCommand(string Action, int? ScreenX = null, int? ScreenY = null);

internal sealed class SingleInstanceCommands : IDisposable
{
    internal static string InstanceName => "MyFences-" + WindowsIdentity.GetCurrent().User!.Value;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _listener;

    public SingleInstanceCommands(Dispatcher dispatcher, Action<InstanceCommand> receive)
    {
        _listener = Task.Run(async () =>
        {
            while (!_stop.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream(InstanceName, PipeDirection.InOut, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(_stop.Token);
                    using var reader = new StreamReader(pipe, leaveOpen: true);
                    using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
                    var line = await reader.ReadLineAsync(_stop.Token);
                    var command = line is null ? null : JsonSerializer.Deserialize<InstanceCommand>(line);
                    if (command is { Action: "new-group" })
                    {
                        _ = dispatcher.BeginInvoke(() => receive(command));
                        await writer.WriteLineAsync("accepted");
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (IOException) { }
                catch (JsonException) { }
            }
        });
    }

    public static bool Send(InstanceCommand command)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", InstanceName, PipeDirection.InOut, PipeOptions.CurrentUserOnly);
            pipe.Connect(5000);
            // The Explorer-launched process owns the user's foreground gesture.
            // Pass it to the existing instance before its UI-thread command runs.
            if (GetNamedPipeServerProcessId(pipe.SafePipeHandle, out var serverProcess))
                AllowSetForegroundWindow(serverProcess);
            using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
            writer.WriteLine(JsonSerializer.Serialize(command));
            // The acknowledgement means the command was queued on the main UI thread.
            using var reader = new StreamReader(pipe, leaveOpen: true);
            return reader.ReadLine() == "accepted";
        }
        catch (IOException) { return false; }
        catch (TimeoutException) { return false; }
    }

    public void Dispose() { _stop.Cancel(); }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint processId);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
