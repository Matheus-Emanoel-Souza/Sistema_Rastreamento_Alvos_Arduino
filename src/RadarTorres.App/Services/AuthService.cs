using System;
using System.Threading.Tasks;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IAuthService"/>. Toda tentativa de login (sucesso ou falha) é
/// registrada no console de eventos (Requisito 7 — "registro de tentativas de acesso");
/// nenhuma senha, hash ou salt é gravado em log (Requisito 11).
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILoggingService _logger;

    public Usuario? CurrentUser { get; private set; }

    public event EventHandler? SessionChanged;

    public AuthService(IUsuarioRepository usuarioRepository, IPasswordHasher passwordHasher, ILoggingService logger)
    {
        _usuarioRepository = usuarioRepository;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public Task<AuthResult> LoginAsync(string login, string senha)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(senha))
        {
            return Task.FromResult(AuthResult.Fail("Informe usuário e senha."));
        }

        Usuario? usuario = _usuarioRepository.GetByLogin(login.Trim());
        if (usuario is null || !usuario.Ativo || !_passwordHasher.Verify(senha, usuario.SenhaHash, usuario.SenhaSalt))
        {
            _logger.Warning($"Tentativa de login malsucedida para o usuário '{login}'.");
            return Task.FromResult(AuthResult.Fail("Usuário ou senha inválidos."));
        }

        usuario.UltimoAcesso = DateTime.Now;
        _usuarioRepository.Update(usuario);

        CurrentUser = usuario;
        _logger.Success($"Login efetuado: {usuario.Nome} ({usuario.Perfil}).");
        SessionChanged?.Invoke(this, EventArgs.Empty);
        return Task.FromResult(AuthResult.Ok(usuario));
    }

    public void Logout()
    {
        if (CurrentUser is null) return;

        _logger.Info($"Logout: {CurrentUser.Nome}.");
        CurrentUser = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<AuthResult> AlterarSenhaAsync(string senhaAtual, string novaSenha)
    {
        if (CurrentUser is null)
        {
            return Task.FromResult(AuthResult.Fail("Nenhum usuário conectado."));
        }

        if (!_passwordHasher.Verify(senhaAtual, CurrentUser.SenhaHash, CurrentUser.SenhaSalt))
        {
            return Task.FromResult(AuthResult.Fail("Senha atual incorreta."));
        }

        if (string.IsNullOrWhiteSpace(novaSenha) || novaSenha.Length < 6)
        {
            return Task.FromResult(AuthResult.Fail("A nova senha deve ter pelo menos 6 caracteres."));
        }

        (string hash, string salt) = _passwordHasher.Hash(novaSenha);
        CurrentUser.SenhaHash = hash;
        CurrentUser.SenhaSalt = salt;
        _usuarioRepository.Update(CurrentUser);

        _logger.Success("Senha alterada com sucesso.");
        return Task.FromResult(AuthResult.Ok(CurrentUser));
    }
}
