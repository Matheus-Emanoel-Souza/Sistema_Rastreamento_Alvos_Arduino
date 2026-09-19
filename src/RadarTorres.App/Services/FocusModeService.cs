using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IFocusModeService"/> baseada em processos do Windows (fechar
/// janelas de outros aplicativos) e em um script PowerShell elevado (isolar a rede).
/// </summary>
/// <remarks>
/// A API pública de modo avião do Windows (<c>Windows.Devices.Radios.Radio</c>) exige que o
/// processo tenha identidade de pacote (app empacotado/MSIX) — um WPF "clássico" como este,
/// distribuído via instalador Inno Setup, não tem essa identidade e a chamada falharia sempre
/// com acesso negado. Por isso o "modo avião" aqui é obtido desligando os adaptadores de rede
/// (Wi-Fi/Ethernet) e os rádios Bluetooth diretamente, o que produz o mesmo efeito prático
/// (máquina isolada da rede) sem depender de empacotamento MSIX.
/// </remarks>
public sealed class FocusModeService : IFocusModeService
{
    /// <summary>
    /// Processos que nunca são fechados pelo modo foco: o essencial do shell do Windows, o
    /// próprio RadarTorres e as ferramentas de desenvolvimento mais comuns (para não derrubar
    /// o ambiente de quem estiver testando esta função a partir da IDE/terminal).
    /// </summary>
    private static readonly HashSet<string> ProcessosEssenciais = new(StringComparer.OrdinalIgnoreCase)
    {
        "RadarTorres.App", "RadarTorres.Launcher",
        "explorer", "dwm", "sihost", "ctfmon", "fontdrvhost",
        "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "SearchApp",
        "TextInputHost", "ApplicationFrameHost", "SystemSettings", "LockApp", "LogonUI",
        "csrss", "wininit", "winlogon", "services", "lsass", "smss", "svchost", "System", "Idle",
        "RuntimeBroker", "SecurityHealthSystray", "SecurityHealthService", "taskhostw",
        "spoolsv", "WmiPrvSE", "conhost", "powershell", "pwsh", "cmd", "WindowsTerminal",
        "dotnet", "devenv", "Code", "Claude",
    };

    private readonly ILoggingService _loggingService;

    public FocusModeService(ILoggingService loggingService)
    {
        _loggingService = loggingService;
    }

    public bool IsActive { get; private set; }

    public Task<FocusModeResultado> EnterFocusModeAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            _loggingService.Info("Modo foco: fechando aplicativos em segundo plano...");
            (int fechados, int falhas) = FecharProcessosEmSegundoPlano();
            _loggingService.Info($"Modo foco: {fechados} janela(s) fechada(s), {falhas} processo(s) não puderam ser fechados.");

            cancellationToken.ThrowIfCancellationRequested();

            (bool redeDesativada, string? aviso) = TentarIsolarRede(ativar: true);
            if (redeDesativada)
            {
                _loggingService.Success("Modo foco: rede desativada (Wi-Fi/Ethernet/Bluetooth).");
            }
            else
            {
                _loggingService.Warning($"Modo foco: não foi possível desativar a rede automaticamente. {aviso}");
            }

            IsActive = true;
            return new FocusModeResultado(fechados, falhas, redeDesativada, aviso);
        }, cancellationToken);

    public Task ExitFocusModeAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            _loggingService.Info("Modo foco: restaurando a rede...");
            (bool ok, string? aviso) = TentarIsolarRede(ativar: false);
            if (ok)
            {
                _loggingService.Success("Modo foco: rede restaurada.");
            }
            else
            {
                _loggingService.Warning($"Modo foco: não foi possível restaurar a rede automaticamente. {aviso}");
            }

            IsActive = false;
        }, cancellationToken);

    /// <summary>
    /// Fecha todo processo com janela visível que não esteja na lista de essenciais. Tenta um
    /// fechamento educado (<see cref="Process.CloseMainWindow"/>) antes de forçar (<see cref="Process.Kill(bool)"/>),
    /// para dar chance de o aplicativo salvar estado/perguntar antes de fechar.
    /// </summary>
    private (int Fechados, int Falhas) FecharProcessosEmSegundoPlano()
    {
        int fechados = 0;
        int falhas = 0;
        int meuProcessoId = Environment.ProcessId;

        foreach (Process processo in Process.GetProcesses())
        {
            using (processo)
            {
                try
                {
                    if (processo.Id == meuProcessoId) continue;
                    if (ProcessosEssenciais.Contains(processo.ProcessName)) continue;
                    if (processo.MainWindowHandle == IntPtr.Zero) continue;

                    if (!processo.CloseMainWindow() || !processo.WaitForExit(2000))
                    {
                        processo.Kill(entireProcessTree: true);
                        processo.WaitForExit(2000);
                    }

                    fechados++;
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                {
                    // Processos do sistema/de outros usuários recusam CloseMainWindow/Kill (acesso
                    // negado) ou já saíram sozinhos entre a enumeração e a tentativa — não é um
                    // erro fatal para o modo foco como um todo, só é contabilizado.
                    falhas++;
                    _loggingService.Warning($"Modo foco: não foi possível fechar '{SafeProcessName(processo)}' ({ex.Message}).");
                }
            }
        }

        return (fechados, falhas);
    }

    private static string SafeProcessName(Process processo)
    {
        try { return processo.ProcessName; }
        catch { return "(desconhecido)"; }
    }

    /// <summary>
    /// Desativa/reativa adaptadores de rede e rádios Bluetooth via um único script PowerShell
    /// elevado (uma única solicitação de UAC para todo o lote, em vez de uma por adaptador).
    /// </summary>
    private (bool Ok, string? Aviso) TentarIsolarRede(bool ativar)
    {
        string acaoAdaptador = ativar ? "Disable-NetAdapter" : "Enable-NetAdapter";
        string acaoBluetooth = ativar ? "Disable-PnpDevice" : "Enable-PnpDevice";

        string script =
            $"{acaoAdaptador} -Name * -Confirm:$false -ErrorAction SilentlyContinue; " +
            $"Get-PnpDevice -Class Bluetooth -ErrorAction SilentlyContinue | {acaoBluetooth} -Confirm:$false -ErrorAction SilentlyContinue";

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{script}\"",
            UseShellExecute = true,
            Verb = "runas",
            WindowStyle = ProcessWindowStyle.Hidden,
        };

        try
        {
            using Process? processo = Process.Start(startInfo);
            if (processo is null)
            {
                return (false, "Não foi possível iniciar o PowerShell elevado.");
            }

            processo.WaitForExit();
            return processo.ExitCode == 0
                ? (true, null)
                : (false, $"O script de rede terminou com código {processo.ExitCode}.");
        }
        catch (Win32Exception ex)
        {
            // Código 1223 = ERROR_CANCELLED: o usuário negou o prompt de UAC.
            string motivo = ex.NativeErrorCode == 1223
                ? "a elevação de privilégios (UAC) foi cancelada pelo usuário."
                : ex.Message;
            return (false, $"Desative manualmente pelo Windows se necessário — {motivo}");
        }
    }
}
