using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Localiza o executável <c>arduino-cli.exe</c> no computador do usuário. O Arduino CLI é uma
/// ferramenta externa — NÃO faz parte do runtime do .NET nem é distribuída junto do
/// RadarTorres — então esta detecção nunca baixa nada automaticamente, apenas procura em
/// locais já existentes no disco (ver <see cref="Locate"/>).
/// </summary>
public interface IArduinoCliLocatorService
{
    /// <summary>
    /// Procura o Arduino CLI, nesta ordem: (1) <paramref name="savedPath"/> informado/salvo
    /// pelo usuário; (2) pasta do próprio aplicativo (cópia local); (3) variável de ambiente
    /// PATH; (4) locais comuns de instalação (Arduino IDE 2.x, WinGet, etc.).
    /// </summary>
    ArduinoCliInfo Locate(string? savedPath);

    /// <summary>Executa <c>arduino-cli version</c> e retorna a versão relatada, ou null se falhar.</summary>
    Task<string?> GetVersionAsync(string cliPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executa <c>arduino-cli board listall</c> para complementar o catálogo estático de
    /// placas com as placas dos "cores" já instalados pelo usuário. Retorna lista vazia (sem
    /// lançar exceção) se o CLI não estiver disponível ou o comando falhar.
    /// </summary>
    Task<System.Collections.Generic.IReadOnlyList<ArduinoBoardOption>> ListInstalledBoardsAsync(string cliPath, CancellationToken cancellationToken = default);
}
