using System.Diagnostics;

namespace ChatApplication;

public static class Program
{
    private static readonly List<Process> ChildProcesses = new();

    public static void Main()
    {
        Console.WriteLine("ChatApplication test launcher");
        Console.WriteLine("S - start server");
        Console.WriteLine("C - start client");
        Console.WriteLine("Q - quit and stop launched processes");

        while (true)
        {
            Console.Write("\nChoose an option: ");
            string? choice = Console.ReadLine()?.Trim().ToUpperInvariant();

            switch (choice)
            {
                case "S":
                    StartProject("Server", "Chat server");
                    break;
                case "C":
                    StartProject("Client", "Chat client");
                    break;
                case "Q":
                    StopChildren();
                    return;
                default:
                    Console.WriteLine("Please choose S, C, or Q.");
                    break;
            }
        }
    }

    private static void StartProject(string projectName, string windowTitle)
    {
        string projectPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..",
            projectName,
            $"{projectName}.csproj");

        projectPath = Path.GetFullPath(projectPath);
        if (!File.Exists(projectPath))
        {
            Console.WriteLine($"Could not find {projectPath}");
            return;
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(projectPath)!,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(projectPath);

        Process? process = Process.Start(startInfo);
        if (process == null)
        {
            Console.WriteLine($"Could not start {windowTitle}.");
            return;
        }

        ChildProcesses.Add(process);
        Console.WriteLine($"{windowTitle} started.");
    }

    private static void StopChildren()
    {
        foreach (Process process in ChildProcesses)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch (InvalidOperationException)
            {
                // The process exited between the check and the cleanup.
            }
        }
    }
}
