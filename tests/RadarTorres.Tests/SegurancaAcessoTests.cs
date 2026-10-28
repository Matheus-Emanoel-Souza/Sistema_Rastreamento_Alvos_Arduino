using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Controle de acesso (Requisito 7/11): a navegação entre telas da <c>ShellWindow</c> deve
/// respeitar a sessão autenticada e o perfil do usuário mesmo quando acionada diretamente via
/// <see cref="ShellViewModel.NavigateCommand"/> — ou seja, sem depender apenas do botão da
/// Sidebar estar oculto para perfis sem permissão (ver <see cref="ShellViewModel"/>,
/// método privado <c>PodeNavegarPara</c>, que repete a checagem de
/// <see cref="IPermissionService.PodeVerMenu"/> no ponto de navegação).
/// </summary>
public sealed class SegurancaAcessoTests
{
    private static ShellViewModel CriarShell(AuthServiceStub auth, out NavigationServiceEspiao navegacao)
    {
        navegacao = new NavigationServiceEspiao();
        return new ShellViewModel(
            auth,
            new LocalizationServiceFalso(),
            new ThemeServiceFalso(),
            navegacao,
            new PermissionService(),
            new PreferenciasUsuarioRepositoryFalso(),
            new SerialCommunicationServiceFalso(),
            new FocusModeServiceFalso());
    }

    // ---------------------------------------------------------------- 1: sem autenticação

    [Fact]
    public void SemSessaoAutenticada_NavegacaoParaQualquerTelaEhBloqueada()
    {
        var auth = new AuthServiceStub(usuario: null);
        ShellViewModel shell = CriarShell(auth, out NavigationServiceEspiao navegacao);

        shell.NavigateCommand.Execute(MenuItem.PainelPrincipal);

        Assert.Null(navegacao.UltimoItemNavegado);
    }

    // ---------------------------------------------------------------- 2: sessão inválida (usuário inativo)

    [Fact]
    public void UsuarioInativo_NavegacaoEhBloqueadaMesmoComCurrentUserPreenchido()
    {
        var usuarioInativo = new Usuario { Id = 5, Nome = "Desligado", Login = "desligado", Perfil = PerfilUsuario.Operador, Ativo = false };
        var auth = new AuthServiceStub(usuarioInativo);
        ShellViewModel shell = CriarShell(auth, out NavigationServiceEspiao navegacao);

        shell.NavigateCommand.Execute(MenuItem.PainelPrincipal);

        Assert.Null(navegacao.UltimoItemNavegado);
    }

    // ---------------------------------------------------------------- 3: acesso autorizado

    [Fact]
    public void Administrador_NavegaParaTelaDeUsuariosComSucesso()
    {
        var admin = new Usuario { Id = 1, Nome = "Admin", Login = "admin", Perfil = PerfilUsuario.Administrador, Ativo = true };
        var auth = new AuthServiceStub(admin);
        ShellViewModel shell = CriarShell(auth, out NavigationServiceEspiao navegacao);

        shell.NavigateCommand.Execute(MenuItem.Usuarios);

        Assert.Equal(MenuItem.Usuarios, navegacao.UltimoItemNavegado);
    }

    [Fact]
    public void OperadorComum_NavegaParaPainelPrincipalComSucesso()
    {
        var operador = new Usuario { Id = 2, Nome = "Operador", Login = "operador", Perfil = PerfilUsuario.Operador, Ativo = true };
        var auth = new AuthServiceStub(operador);
        ShellViewModel shell = CriarShell(auth, out NavigationServiceEspiao navegacao);

        shell.NavigateCommand.Execute(MenuItem.PainelPrincipal);

        Assert.Equal(MenuItem.PainelPrincipal, navegacao.UltimoItemNavegado);
    }

    // ---------------------------------------------------------------- 4: usuário sem a permissão exigida

    [Fact]
    public void UsuarioComumTentaNavegarParaTelaDeUsuarios_AcessoEhNegadoMesmoViaComandoDireto()
    {
        // Simula o cenário descrito no pedido de auditoria: a Sidebar esconde o botão
        // "Usuários" para quem não é Administrador, mas nada deveria impedir que o mesmo
        // RelayCommand seja acionado diretamente (ex.: outro gatilho de UI, atalho, teste).
        var operador = new Usuario { Id = 2, Nome = "Operador", Login = "operador", Perfil = PerfilUsuario.Operador, Ativo = true };
        var auth = new AuthServiceStub(operador);
        ShellViewModel shell = CriarShell(auth, out NavigationServiceEspiao navegacao);

        shell.NavigateCommand.Execute(MenuItem.Usuarios);

        Assert.Null(navegacao.UltimoItemNavegado);
    }

    [Fact]
    public void Visualizador_NaoConsegueNavegarParaTreinamentoDoControlador()
    {
        var visualizador = new Usuario { Id = 3, Nome = "Visualizador", Login = "visualizador", Perfil = PerfilUsuario.Visualizador, Ativo = true };
        var auth = new AuthServiceStub(visualizador);
        ShellViewModel shell = CriarShell(auth, out NavigationServiceEspiao navegacao);

        shell.NavigateCommand.Execute(MenuItem.TreinamentoControlador);

        Assert.Null(navegacao.UltimoItemNavegado);
    }

    // ---------------------------------------------------------------- Regra de permissão isolada (PermissionService)

    [Theory]
    [InlineData(PerfilUsuario.Administrador, true)]
    [InlineData(PerfilUsuario.Operador, false)]
    [InlineData(PerfilUsuario.Visualizador, false)]
    public void PodeVerMenu_TelaDeUsuarios_SomenteAdministrador(PerfilUsuario perfil, bool esperado)
    {
        var permissao = new PermissionService();
        Assert.Equal(esperado, permissao.PodeVerMenu(perfil, MenuItem.Usuarios));
    }
}

// ---------------------------------------------------------------------- Dublês desta suíte

/// <summary>Dublê de <see cref="IAuthService"/> com controle total do usuário/sessão atual
/// (diferente de <c>AuthServiceFalso</c> de <c>CadastroAlvoModalTests.cs</c>, que sempre tem
/// um usuário Ativo — aqui também é possível simular ausência de sessão e usuário inativo).</summary>
internal sealed class AuthServiceStub : IAuthService
{
    public AuthServiceStub(Usuario? usuario) => CurrentUser = usuario;

    public Usuario? CurrentUser { get; private set; }

    public event EventHandler? SessionChanged;

    public Task<AuthResult> LoginAsync(string login, string senha) =>
        Task.FromResult(CurrentUser is null ? AuthResult.Fail("sem usuário configurado no stub") : AuthResult.Ok(CurrentUser));

    public void Logout()
    {
        CurrentUser = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<AuthResult> AlterarSenhaAsync(string senhaAtual, string novaSenha) =>
        Task.FromResult(CurrentUser is null ? AuthResult.Fail("sem sessão") : AuthResult.Ok(CurrentUser));

    public Task<AuthResult> RedefinirSenhaAsync(int usuarioId, string novaSenha) =>
        Task.FromResult(CurrentUser is null ? AuthResult.Fail("sem sessão") : AuthResult.Ok(CurrentUser));
}

/// <summary>Espiona qual item foi efetivamente navegado — se a checagem de permissão em
/// <see cref="ShellViewModel"/> falhar ao bloquear, <see cref="UltimoItemNavegado"/> acusa.</summary>
internal sealed class NavigationServiceEspiao : INavigationService
{
    public MenuItem? CurrentItem { get; private set; }

    public MenuItem? UltimoItemNavegado { get; private set; }

    public event EventHandler<MenuItem>? Navigated;

    public void SetHost(System.Windows.Controls.ContentControl host)
    {
    }

    public void NavigateTo(MenuItem item)
    {
        UltimoItemNavegado = item;
        CurrentItem = item;
        Navigated?.Invoke(this, item);
    }
}

internal sealed class LocalizationServiceFalso : ILocalizationService
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string[] AvailableLanguages => new[] { "pt-BR" };

    public string CurrentLanguage => "pt-BR";

    public string this[string key] => key;

    public void SetLanguage(string languageCode)
    {
    }
}

internal sealed class ThemeServiceFalso : IThemeService
{
    public TemaPreferido CurrentTheme { get; private set; } = TemaPreferido.Escuro;

    public void ApplyTheme(TemaPreferido tema) => CurrentTheme = tema;
}

internal sealed class PreferenciasUsuarioRepositoryFalso : IPreferenciasUsuarioRepository
{
    public PreferenciasUsuario? GetByUsuarioId(int usuarioId) => null;

    public void Salvar(PreferenciasUsuario preferencias)
    {
    }
}

internal sealed class SerialCommunicationServiceFalso : ISerialCommunicationService
{
    public ConnectionState State => ConnectionState.Disconnected;

    public string? CurrentPortName => null;

    public int CurrentBaudRate => 0;

    public event EventHandler<SerialMessage>? MessageReceived;

    public event EventHandler<ConnectionState>? ConnectionStateChanged;

    public event EventHandler<string>? CommunicationError;

    public IReadOnlyList<string> GetAvailablePorts() => Array.Empty<string>();

    public Task<bool> ConnectAsync(string portName, int baudRate, CancellationToken cancellationToken = default) => Task.FromResult(false);

    public void Disconnect()
    {
    }

    public Task<bool> SendCommandAsync(string command) => Task.FromResult(false);

    public void Dispose()
    {
    }
}

internal sealed class FocusModeServiceFalso : IFocusModeService
{
    public bool IsActive { get; private set; }

    public Task<FocusModeResultado> EnterFocusModeAsync(CancellationToken cancellationToken = default)
    {
        IsActive = true;
        return Task.FromResult(new FocusModeResultado(0, 0, true, null));
    }

    public Task ExitFocusModeAsync(CancellationToken cancellationToken = default)
    {
        IsActive = false;
        return Task.CompletedTask;
    }
}
