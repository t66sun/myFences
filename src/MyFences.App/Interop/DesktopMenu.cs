using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32;
using MyFences.App.Ui;

namespace MyFences.App.Interop;

internal static class DesktopMenu
{
    internal const string VerbKey = @"Software\Classes\DesktopBackground\shell\MyFences.NewGroup";
    internal const string StateKey = @"Software\MyFences\DesktopMenu";
    internal const string PackageName = "MyFences.DesktopMenu";
    private static string Executable => Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.");

    public static string Status()
    {
        using var state = Registry.CurrentUser.OpenSubKey(StateKey);
        var path = state?.GetValue("ExecutablePath") as string;
        var package = RunPowerShell("Get-AppxPackage -Name " + Quote(PackageName) + " | Select-Object -ExpandProperty PackageFullName").Trim();
        using var verb = Registry.CurrentUser.OpenSubKey(VerbKey + @"\command");
        var classicCommand = verb?.GetValue("") as string;
        if (package.Length == 0 && classicCommand is null) return Text.Get("menuOff");
        var status = package.Length != 0 ? Text.Get("menuModern") : Text.Get("menuClassic");
        if (path is null) return status + "\n" + Text.Get("menuUnrecorded");
        return status + "\n" + path + (string.Equals(path, Executable, StringComparison.OrdinalIgnoreCase) ? "" : "\n" + Text.Get("menuMoved"));
    }

    public static void Enable()
        => EnableForExecutable(Executable);

    internal static void EnableForExecutable(string executable)
    {
        var root = Path.GetDirectoryName(Path.GetFullPath(executable))!;
        var package = Path.Combine(root, "MyFences.DesktopMenu.msix");
        var certificate = Path.Combine(root, "MyFences.DesktopMenu.cer");
        if (!File.Exists(package) || !File.Exists(Path.Combine(root, "MyFences.ShellExtension.dll")))
            throw new InvalidOperationException(Text.Get("menuPackageMissing"));
        {
            if (File.Exists(certificate))
            {
                using var cert = X509CertificateLoader.LoadCertificateFromFile(certificate);
                using var store = new X509Store(StoreName.TrustedPeople, StoreLocation.LocalMachine);
                store.Open(OpenFlags.ReadOnly);
                if (store.Certificates.Find(X509FindType.FindByThumbprint, cert.Thumbprint, false).Count == 0)
                    throw new InvalidOperationException(Text.Get("menuCertificate") + "\n" + Path.Combine(root, "trust-desktop-menu-certificate.ps1"));
            }
            // A sparse package's external location can remain unchanged on a same-version add.
            // Remove only our own package, then explicitly register this executable's directory.
            File.WriteAllText(Path.Combine(root, "MyFences.DesktopMenu.title"), Text.Get("menuCommand"), System.Text.Encoding.Unicode);
            RunPowerShell("Get-AppxPackage -Name " + Quote(PackageName) + " | Remove-AppxPackage -ErrorAction Stop; Add-AppxPackage -Path " + Quote(package) + " -ExternalLocation " + Quote(root) + " -ErrorAction Stop");
            Registry.CurrentUser.DeleteSubKeyTree(VerbKey, false);
        }
        using var state = Registry.CurrentUser.CreateSubKey(StateKey);
        state.SetValue("ExecutablePath", executable);
        state.SetValue("Mode", "modern");
        SHChangeNotify(0x08000000, 0, 0, 0);
    }

    public static void Disable()
    {
        RunPowerShell("Get-AppxPackage -Name " + Quote(PackageName) + " | Remove-AppxPackage -ErrorAction Stop");
        Registry.CurrentUser.DeleteSubKeyTree(VerbKey, false);
        Registry.CurrentUser.DeleteSubKeyTree(StateKey, false);
        SHChangeNotify(0x08000000, 0, 0, 0);
    }

    private static string Quote(string value) => "'" + value.Replace("'", "''") + "'";
    private static string RunPowerShell(string command)
    {
        var start = new ProcessStartInfo("powershell.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
        start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command)));
        using var process = Process.Start(start)!;
        var errorTask = process.StandardError.ReadToEndAsync();
        var output = process.StandardOutput.ReadToEnd(); process.WaitForExit();
        var error = errorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0) throw new InvalidOperationException(error.Trim());
        return output;
    }

    [DllImport("shell32.dll")] private static extern void SHChangeNotify(uint eventId, uint flags, nint first, nint second);
}
