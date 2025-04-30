using System;
using System.Diagnostics;
using System.Threading;

namespace MinecraftMemoryMonitor;

static class Program
{
    // Memory threshold: 32GB (in bytes)
    private const long MaxMemoryBytes = 32L * 1024 * 1024 * 1024;
    // Memory check interval (milliseconds)
    private const int CheckIntervalMs = 5000;
    // Exit code
    private const int ExitCode = -65535;

    public static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("Usage: MinecraftMemoryMonitor.exe <program path> [arguments...]");
            return 1;
        }

        // Build process information
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = args[0] // First argument is the program to execute
        };

        // Add remaining command line arguments
        for (int i = 1; i < args.Length; i++)
        {
            psi.ArgumentList.Add(args[i]);
        }

        // Configure redirects to ensure standard input/output works normally
        psi.UseShellExecute = false;
        psi.RedirectStandardInput = true;
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;

        // Start the process
        using Process process = new Process();
        process.StartInfo = psi;
                
        // Redirection event handlers to ensure real-time output display
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                Console.WriteLine(e.Data);
        };
                
        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                Console.Error.WriteLine(e.Data);
        };

        // Start the process
        process.Start();
                
        // Start asynchronous reading of output
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Pass standard input
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                // Read input from console and forward to process
                string? line;
                while ((line = Console.ReadLine()) != null)
                {
                    process.StandardInput.WriteLine(line);
                    process.StandardInput.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error processing standard input: {ex.Message}");
            }
        });

        // Memory monitoring thread
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try
            {
                while (!process.HasExited)
                {
                    // Refresh process information
                    process.Refresh();
                            
                    // Get committed memory (working set + page file usage)
                    // In Windows, PrivateMemorySize64 typically represents committed memory
                    long committedMemory = process.PrivateMemorySize64;
                            
                    // Check if exceeding threshold
                    if (committedMemory > MaxMemoryBytes)
                    {
                        Console.Error.WriteLine($"Memory usage exceeded threshold (32GB), current: {committedMemory / (1024.0 * 1024 * 1024):F2} GB");
                                
                        // Try to gracefully close the process
                        try { process.Kill(); }
                        catch
                        {
                            // Ignored
                        }

                        // Set exit code and exit
                        Environment.Exit(ExitCode);
                    }
                            
                    // Wait for next check
                    Thread.Sleep(CheckIntervalMs);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Memory monitoring thread error: {ex.Message}");
            }
        });

        // Wait for process to exit
        process.WaitForExit();
        return process.ExitCode;
    }
}