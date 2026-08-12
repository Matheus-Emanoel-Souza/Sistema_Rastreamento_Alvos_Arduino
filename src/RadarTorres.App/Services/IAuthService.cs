using System;
using System.Threading.Tasks;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Resultado de uma tentativa de login.</summary>
public sealed record AuthResult(bool Success, string? ErrorMessage, Usuario? Usuario)
{
    public static AuthResult Ok(Usuario usuario) => new(true, null, usuario);
    public static AuthResult Fail(string message) => new(false, message, null);
}

/// <summary>
/// Controle de sessão do aplicativo: login, logout e usuário atualmente conectado.
/// Sessão é local ao processo (sem persistência entre execuções — cada abertura do app exige
/// login novamente, conforme esperado de um controle de acesso).
/// </summary>
public interface IAuthService
{
    /// <summary>Usuário autenticado no momento, ou <c>null</c> se ninguém logado.</summary>
    Usuario? CurrentUser { get; }

    /// <summary>Disparado após login bem-sucedido ou logout.</summary>
    event EventHandler? SessionChanged;

    Task<AuthResult> LoginAsync(string login, string senha);

    void Logout();

    /// <summary>Altera a senha do usuário atualmente logado, validando a senha atual antes.</summary>
    Task<AuthResult> AlterarSenhaAsync(string senhaAtual, string novaSenha);
}
