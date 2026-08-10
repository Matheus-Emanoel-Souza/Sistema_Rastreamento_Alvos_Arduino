using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace RadarTorres.Launcher;

/// <summary>
/// Launcher avulso: localiza o RadarTorres já instalado neste computador e o
/// abre, sem exigir que o usuário procure o atalho no Menu Iniciar.
///
/// Não instala nada e não é o instalador (isso é o installer\RadarTorres.iss,
/// que gera dist\Setup.exe) — apenas encontra e inicia o .exe já instalado.
/// </summary>
internal static partial class Program
{
    // Nome do executável principal e AppId, iguais aos usados em
    // installer\RadarTorres.iss (#define MyAppExeName / #define MyAppId).
    // Mantidos sincronizados manualmente: são poucos e mudam raramente.
    private const string AppExeName = "RadarTorres.App.exe";
    private const string AppId = "6D13EAAB-61F6-4AE8-97B7-34498455E000";

    [STAThread]
    private static int Main()
    {
        string? exePath = FindInstalledExecutable();

        if (exePath is null)
        {
            ShowError(
                "Não foi possível localizar o RadarTorres instalado neste computador.\n" +
                "Instale (ou reinstale) o aplicativo usando o Setup.exe oficial.\n\n" +
                "Could not find RadarTorres installed on this computer.\n" +
                "Please install (or reinstall) it using the official Setup.exe.");
            return 1;
        }

        try
        {
            Process.Start(new ProcessStartInfo(exePath)
            {
                WorkingDirectory = Path.GetDirectoryName(exePath) ?? string.Empty,
                UseShellExecute = true,
            });
            return 0;
        }
        catch (Exception ex)
        {
            ShowError($"Falha ao abrir o RadarTorres:\n{ex.Message}\n\nFailed to launch RadarTorres:\n{ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Procura o executável instalado em duas etapas: primeiro no caminho
    /// padrão de instalação (o caso comum), depois consultando o registro do
    /// Windows (Inno Setup registra o InstallLocation lá), para cobrir quem
    /// escolheu uma pasta de instalação customizada.
    /// </summary>
    private static string? FindInstalledExecutable()
    {
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string defaultPath = Path.Combine(programFiles, "RadarTorres", AppExeName);
        if (File.Exists(defaultPath))
        {
            return defaultPath;
        }

        string? installLocation = FindInstallLocationFromRegistry();
        if (installLocation is not null)
        {
            string candidate = Path.Combine(installLocation, AppExeName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Lê a entrada de "Programas e Recursos" criada pelo Inno Setup
    /// (installer\RadarTorres.iss) para descobrir onde o app foi instalado.
    /// Checa tanto a visão de 64 bits do registro quanto a WOW6432Node, já
    /// que isso pode variar conforme a máquina/versão do Windows.
    /// </summary>
    private static string? FindInstallLocationFromRegistry()
    {
        string[] uninstallKeyPaths =
        [
            $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{{{AppId}}}_is1",
            $@"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\{{{AppId}}}_is1",
        ];

        foreach (string keyPath in uninstallKeyPaths)
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(keyPath);
            if (key?.GetValue("InstallLocation") is string location && !string.IsNullOrWhiteSpace(location))
            {
                return location;
            }
        }

        return null;
    }

    // P/Invoke direto (em vez de MessageBox do WinForms/WPF) para manter o
    // launcher leve e compatível com Native AOT — ver comentário no .csproj.
    [LibraryImport("user32.dll", EntryPoint = "MessageBoxW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int MessageBoxW(nint hWnd, string text, string caption, uint type);

    private static void ShowError(string message)
    {
        const uint MB_OK = 0x0;
        const uint MB_ICONERROR = 0x10;
        MessageBoxW(0, message, "RadarTorres", MB_OK | MB_ICONERROR);
    }
}
