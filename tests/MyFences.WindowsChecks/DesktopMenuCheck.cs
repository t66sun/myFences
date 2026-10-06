using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using MyFences.App.Interop;

namespace MyFences.WindowsChecks;

internal static class DesktopMenuCheck
{
    public static int Run(Application app, string firstExecutable, string movedExecutable)
    {
        var results = new Dictionary<string, object>();
        try
        {
            TextLanguage();
            DesktopMenu.EnableForExecutable(firstExecutable);
            CheckLocation(firstExecutable);
            results["firstLocationRegistered"] = true;
            DesktopMenu.EnableForExecutable(movedExecutable);
            CheckLocation(movedExecutable);
            results["sameVersionLocationUpdated"] = true;
            DesktopMenu.Disable();
            if (PackageLocation().Length != 0 || Registry.CurrentUser.OpenSubKey(DesktopMenu.StateKey) is not null)
                throw new InvalidOperationException("Removing the desktop menu left package or metadata registration.");
            if (DesktopMenu.Status() != MyFences.App.Ui.Text.Get("menuOff"))
                throw new InvalidOperationException("Disabled menu status is incorrect.");
            results["removed"] = true;
            DesktopMenu.EnableForExecutable(firstExecutable);
            CheckLocation(firstExecutable);
            results["reEnabled"] = true;
            results["success"] = true;
            return 0;
        }
        catch (Exception error) { results["success"] = false; results["error"] = error.ToString(); return 1; }
        finally
        {
            Directory.CreateDirectory(MyFences.App.Program.DataDirectory);
            File.WriteAllText(Path.Combine(MyFences.App.Program.DataDirectory, "desktop-menu-result.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine(JsonSerializer.Serialize(results));
            app.Shutdown();
        }

        void CheckLocation(string executable)
        {
            using var metadata = Registry.CurrentUser.OpenSubKey(DesktopMenu.StateKey);
            if (!string.Equals(metadata?.GetValue("ExecutablePath") as string, Path.GetFullPath(executable), StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Menu command still points to another executable.");
            var expected = Path.GetDirectoryName(Path.GetFullPath(executable))!.TrimEnd('\\');
            if (!string.Equals(PackageLocation().TrimEnd('\\'), expected, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The sparse package external location was not updated.");
        }
        static void TextLanguage() => MyFences.App.Ui.Text.Language = "en";
        static string PackageLocation()
        {
            var start = new ProcessStartInfo("powershell.exe") { UseShellExecute=false, CreateNoWindow=true, RedirectStandardOutput=true, RedirectStandardError=true };
            start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-NonInteractive"); start.ArgumentList.Add("-Command");
            start.ArgumentList.Add("Get-AppxPackage -Name MyFences.DesktopMenu | Select-Object -ExpandProperty PackageFullName");
            using var process = Process.Start(start)!;
            var errors = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd(); process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException(errors.GetAwaiter().GetResult());
            var fullName = output.Trim();
            if (fullName.Length == 0) return "";
            uint length = 0;
            var status = GetPackagePathByFullName2(fullName, 5, ref length, null);
            if (status != 122) throw new InvalidOperationException("Cannot query package external location: " + status);
            var path = new StringBuilder((int)length);
            status = GetPackagePathByFullName2(fullName, 5, ref length, path);
            if (status != 0) throw new InvalidOperationException("Cannot read package external location: " + status);
            return path.ToString();
        }
    }

    [DllImport("api-ms-win-appmodel-runtime-l1-1-3.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetPackagePathByFullName2(string fullName, int pathType, ref uint length, StringBuilder? path);
}
