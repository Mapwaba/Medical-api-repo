using System.Diagnostics;

var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Could not resolve exe path.");
var repoRoot = FindRepoRoot(new FileInfo(exePath).Directory!);
var scriptPath = Path.Combine(repoRoot, "run-all.ps1");

if (!File.Exists(scriptPath))
{
    Console.Error.WriteLine($"Could not find run-all.ps1 (looked next to and above {exePath}).");
    Pause();
    return 1;
}

var psi = new ProcessStartInfo("powershell.exe")
{
    WorkingDirectory = repoRoot,
    UseShellExecute = false,
};
psi.ArgumentList.Add("-NoProfile");
psi.ArgumentList.Add("-ExecutionPolicy");
psi.ArgumentList.Add("Bypass");
psi.ArgumentList.Add("-File");
psi.ArgumentList.Add(scriptPath);
foreach (var arg in args)
{
    psi.ArgumentList.Add(arg);
}

using var process = Process.Start(psi)!;
process.WaitForExit();

Pause();
return process.ExitCode;

static string FindRepoRoot(DirectoryInfo start)
{
    for (var dir = start; dir is not null; dir = dir.Parent!)
    {
        if (File.Exists(Path.Combine(dir.FullName, "run-all.ps1")))
        {
            return dir.FullName;
        }
    }

    return start.FullName;
}

static void Pause()
{
    Console.WriteLine();
    Console.WriteLine("Press any key to close this window...");
    Console.ReadKey(true);
}
