using System.Diagnostics;
using System.Text;
using Application.Features.ESign.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services.ESign;

public sealed class WordToPdfConverter(IConfiguration configuration) : IWordToPdfConverter
{
    private static readonly SemaphoreSlim ConversionGate = new(1, 1);
    private static readonly TimeSpan ConversionTimeout = TimeSpan.FromSeconds(60);

    public async Task<byte[]> ConvertAsync(
        byte[] documentContent,
        string documentFileName,
        CancellationToken cancellationToken)
    {
        if (documentContent.Length == 0)
        {
            throw new InvalidOperationException("The Word document is empty.");
        }

        await ConversionGate.WaitAsync(cancellationToken);
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"gigbridge-pdf-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(workingDirectory);
            var inputPath = Path.Combine(workingDirectory, SafeDocxFileName(documentFileName));
            var outputPath = Path.ChangeExtension(inputPath, ".pdf");
            await File.WriteAllBytesAsync(inputPath, documentContent, cancellationToken);

            var libreOfficePath = ResolveLibreOfficePath();
            if (libreOfficePath is not null)
            {
                await ConvertWithLibreOfficeAsync(
                    libreOfficePath,
                    inputPath,
                    workingDirectory,
                    cancellationToken);
            }
            else if (OperatingSystem.IsWindows())
            {
                try
                {
                    await ConvertWithMicrosoftWordAsync(inputPath, outputPath, cancellationToken);
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // A prior conversion may have left a broken/orphaned WINWORD.EXE behind
                    // (COM-activated, not a child process, so process-kill on timeout can't
                    // reach it). Sweep it and retry once before giving up.
                    KillOrphanedWinword();
                    await ConvertWithMicrosoftWordAsync(inputPath, outputPath, cancellationToken);
                }
            }
            else
            {
                throw new InvalidOperationException(
                    "PDF conversion is unavailable. Install LibreOffice Writer or configure PdfConversion:LibreOfficePath.");
            }

            var pdf = await File.ReadAllBytesAsync(outputPath, cancellationToken);
            if (pdf.Length < 5 || !pdf.AsSpan(0, 5).SequenceEqual("%PDF-"u8))
            {
                throw new InvalidOperationException("The document converter did not produce a valid PDF.");
            }

            return pdf;
        }
        finally
        {
            try
            {
                if (Directory.Exists(workingDirectory)) Directory.Delete(workingDirectory, true);
            }
            catch (IOException)
            {
                // Temporary output is best-effort cleanup and contains no persisted artifact.
            }
            finally
            {
                ConversionGate.Release();
            }
        }
    }

    private string? ResolveLibreOfficePath()
    {
        var configuredPath = configuration["PdfConversion:LibreOfficePath"]
            ?? Environment.GetEnvironmentVariable("LIBREOFFICE_PATH");
        var candidates = new[]
        {
            configuredPath,
            OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LibreOffice", "program", "soffice.exe")
                : "/usr/bin/libreoffice",
            OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LibreOffice", "program", "soffice.exe")
                : "/usr/bin/soffice"
        };

        return candidates.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path));
    }

    private static async Task ConvertWithLibreOfficeAsync(
        string executable,
        string inputPath,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        var profileDirectory = Path.Combine(outputDirectory, "libreoffice-profile");
        Directory.CreateDirectory(profileDirectory);
        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--nologo");
        startInfo.ArgumentList.Add("--nofirststartwizard");
        startInfo.ArgumentList.Add($"-env:UserInstallation={new Uri(profileDirectory + Path.DirectorySeparatorChar).AbsoluteUri}");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add("pdf:writer_pdf_Export");
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outputDirectory);
        startInfo.ArgumentList.Add(inputPath);
        await RunProcessAsync(startInfo, cancellationToken);
    }

    private static async Task ConvertWithMicrosoftWordAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var escapedInput = inputPath.Replace("'", "''", StringComparison.Ordinal);
        var escapedOutput = outputPath.Replace("'", "''", StringComparison.Ordinal);
        var script = $$"""
            $ErrorActionPreference = 'Stop'
            Get-Process -Name WINWORD -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
            $word = $null
            $document = $null
            try {
                $word = New-Object -ComObject Word.Application
                $word.Visible = $false
                $word.DisplayAlerts = 0
                $document = $word.Documents.Open('{{escapedInput}}', $false, $true)
                $document.ExportAsFixedFormat('{{escapedOutput}}', 17)
            } finally {
                if ($null -ne $document) { try { $document.Close($false) } catch {} }
                if ($null -ne $word) { try { $word.Quit() } catch {} }
                Get-Process -Name WINWORD -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
            }
            """;
        var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
        var startInfo = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-EncodedCommand");
        startInfo.ArgumentList.Add(encodedScript);
        await RunProcessAsync(startInfo, cancellationToken);
    }

    private static async Task RunProcessAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken)
    {
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The document converter could not be started.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ConversionTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            process.Kill(true);
            KillOrphanedWinword();
            throw new TimeoutException("PDF conversion exceeded the 60-second limit.");
        }

        var standardOutput = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = await process.StandardError.ReadToEndAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"PDF conversion failed with exit code {process.ExitCode}: {standardError}{standardOutput}");
        }
    }

    /// <summary>
    /// WINWORD.EXE is DCOM-activated, not a child process of powershell.exe, so killing the
    /// powershell process tree on timeout doesn't reach it. A surviving broken instance can
    /// poison the next conversion's "New-Object -ComObject Word.Application" (attaches via
    /// ROT instead of spawning fresh) and its "$word.Quit()" then fails with an RPC error.
    /// Best-effort cleanup only — never let this mask the real conversion error.
    /// </summary>
    private static void KillOrphanedWinword()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            foreach (var process in Process.GetProcessesByName("WINWORD"))
            {
                using (process)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (Exception)
                    {
                        // Best-effort; nothing more to do if it won't die.
                    }
                }
            }
        }
        catch (Exception)
        {
            // Best-effort; process enumeration failures shouldn't fail the conversion.
        }
    }

    private static string SafeDocxFileName(string fileName)
    {
        var safeName = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "GigBridge-contract";
        return $"{safeName}.docx";
    }
}
