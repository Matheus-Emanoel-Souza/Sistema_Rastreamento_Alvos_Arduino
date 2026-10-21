using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da tela "Usuários" (menu exclusivo do Administrador — ver
/// <see cref="IPermissionService.PodeVerMenu"/>). Lista todos os usuários cadastrados e permite
/// ao Administrador cadastrar novos usuários, editar qualquer campo de um usuário existente e
/// redefinir a senha de qualquer usuário sem precisar da senha atual. Edição dos próprios dados
/// (nome/email/idade) por um usuário comum é feita em <see cref="ProfileViewModel"/>, não aqui.
/// </summary>
public sealed class UsuariosViewModel : ViewModelBase
{
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IAuthService _authService;
    private readonly IPermissionService _permissionService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILoggingService _logger;

    public ObservableCollection<Usuario> Itens { get; } = new();

    public PerfilUsuario[] PerfisDisponiveis { get; } = (PerfilUsuario[])Enum.GetValues(typeof(PerfilUsuario));

    public bool PodeGerenciar => _permissionService.PodeGerenciarUsuarios(_authService.CurrentUser?.Perfil ?? PerfilUsuario.Visualizador);

    public RelayCommand NovoCommand { get; }
    public RelayCommand SalvarCommand { get; }
    public RelayCommand RedefinirSenhaCommand { get; }

    public UsuariosViewModel(
        IUsuarioRepository usuarioRepository,
        IAuthService authService,
        IPermissionService permissionService,
        IPasswordHasher passwordHasher,
        ILoggingService logger)
    {
        _usuarioRepository = usuarioRepository;
        _authService = authService;
        _permissionService = permissionService;
        _passwordHasher = passwordHasher;
        _logger = logger;

        NovoCommand = new RelayCommand(_ => IniciarNovo(), _ => PodeGerenciar);
        SalvarCommand = new RelayCommand(async _ => await SalvarAsync(), _ => PodeGerenciar);
        RedefinirSenhaCommand = new RelayCommand(async _ => await RedefinirSenhaAsync(), _ => PodeGerenciar && SelectedUsuario is not null);
    }

    public void Reload()
    {
        Itens.Clear();
        foreach (Usuario usuario in _usuarioRepository.GetAll().OrderBy(u => u.Nome))
        {
            Itens.Add(usuario);
        }
    }

    // ---------------------------------------------------------------- Seleção / formulário

    private Usuario? _selectedUsuario;
    public Usuario? SelectedUsuario
    {
        get => _selectedUsuario;
        set
        {
            if (SetProperty(ref _selectedUsuario, value) && value is not null)
            {
                CarregarParaEdicao(value);
            }
            RelayCommand.RaiseCanExecuteChangedForAll();
        }
    }

    private int _formId;

    public bool IsNovoUsuario => _formId == 0;

    private string _formNome = string.Empty;
    public string FormNome
    {
        get => _formNome;
        set => SetProperty(ref _formNome, value);
    }

    private string _formEmail = string.Empty;
    public string FormEmail
    {
        get => _formEmail;
        set => SetProperty(ref _formEmail, value);
    }

    private string _formIdade = string.Empty;
    public string FormIdade
    {
        get => _formIdade;
        set => SetProperty(ref _formIdade, value);
    }

    private string _formLogin = string.Empty;
    public string FormLogin
    {
        get => _formLogin;
        set => SetProperty(ref _formLogin, value);
    }

    private PerfilUsuario _formPerfil = PerfilUsuario.Visualizador;
    public PerfilUsuario FormPerfil
    {
        get => _formPerfil;
        set => SetProperty(ref _formPerfil, value);
    }

    private bool _formAtivo = true;
    public bool FormAtivo
    {
        get => _formAtivo;
        set => SetProperty(ref _formAtivo, value);
    }

    private string _formSenha = string.Empty;
    public string FormSenha
    {
        get => _formSenha;
        set => SetProperty(ref _formSenha, value);
    }

    private string _formConfirmarSenha = string.Empty;
    public string FormConfirmarSenha
    {
        get => _formConfirmarSenha;
        set => SetProperty(ref _formConfirmarSenha, value);
    }

    private string? _mensagemFormulario;
    public string? MensagemFormulario
    {
        get => _mensagemFormulario;
        set => SetProperty(ref _mensagemFormulario, value);
    }

    private bool _sucessoFormulario;
    public bool SucessoFormulario
    {
        get => _sucessoFormulario;
        set => SetProperty(ref _sucessoFormulario, value);
    }

    private void IniciarNovo()
    {
        _selectedUsuario = null;
        OnPropertyChanged(nameof(SelectedUsuario));

        _formId = 0;
        FormNome = string.Empty;
        FormEmail = string.Empty;
        FormIdade = string.Empty;
        FormLogin = string.Empty;
        FormPerfil = PerfilUsuario.Visualizador;
        FormAtivo = true;
        FormSenha = string.Empty;
        FormConfirmarSenha = string.Empty;
        MensagemFormulario = null;
        OnPropertyChanged(nameof(IsNovoUsuario));
    }

    private void CarregarParaEdicao(Usuario usuario)
    {
        _formId = usuario.Id;
        FormNome = usuario.Nome;
        FormEmail = usuario.Email;
        FormIdade = usuario.Idade?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        FormLogin = usuario.Login;
        FormPerfil = usuario.Perfil;
        FormAtivo = usuario.Ativo;
        FormSenha = string.Empty;
        FormConfirmarSenha = string.Empty;
        MensagemFormulario = null;
        OnPropertyChanged(nameof(IsNovoUsuario));
    }

    private async Task SalvarAsync()
    {
        SucessoFormulario = false;

        if (string.IsNullOrWhiteSpace(FormNome) || string.IsNullOrWhiteSpace(FormLogin))
        {
            MensagemFormulario = "Informe ao menos nome e login.";
            return;
        }

        int? idade = null;
        if (!string.IsNullOrWhiteSpace(FormIdade))
        {
            if (!int.TryParse(FormIdade, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idadeValor) || idadeValor < 0)
            {
                MensagemFormulario = "Idade inválida.";
                return;
            }
            idade = idadeValor;
        }

        Usuario? existenteComMesmoLogin = _usuarioRepository.GetByLogin(FormLogin.Trim());
        if (existenteComMesmoLogin is not null && existenteComMesmoLogin.Id != _formId)
        {
            MensagemFormulario = "Já existe um usuário com esse login.";
            return;
        }

        if (IsNovoUsuario)
        {
            if (string.IsNullOrWhiteSpace(FormSenha) || FormSenha.Length < 6)
            {
                MensagemFormulario = "A senha deve ter pelo menos 6 caracteres.";
                return;
            }
            if (FormSenha != FormConfirmarSenha)
            {
                MensagemFormulario = "A confirmação não corresponde à senha.";
                return;
            }

            (string hash, string salt) = _passwordHasher.Hash(FormSenha);
            Usuario novo = _usuarioRepository.Add(new Usuario
            {
                Nome = FormNome.Trim(),
                Email = FormEmail.Trim(),
                Idade = idade,
                Login = FormLogin.Trim(),
                SenhaHash = hash,
                SenhaSalt = salt,
                Perfil = FormPerfil,
                Ativo = FormAtivo,
                DataCriacao = DateTime.Now
            });

            _logger.Success($"Usuário criado: {novo.Nome} ({novo.Login}), perfil {novo.Perfil}.");
            MensagemFormulario = "Usuário criado com sucesso.";
        }
        else
        {
            Usuario? usuario = _usuarioRepository.GetById(_formId);
            if (usuario is null)
            {
                MensagemFormulario = "Usuário não encontrado.";
                return;
            }

            usuario.Nome = FormNome.Trim();
            usuario.Email = FormEmail.Trim();
            usuario.Idade = idade;
            usuario.Login = FormLogin.Trim();
            usuario.Perfil = FormPerfil;
            usuario.Ativo = FormAtivo;
            _usuarioRepository.Update(usuario);

            _logger.Success($"Usuário atualizado: {usuario.Nome} ({usuario.Login}).");
            MensagemFormulario = "Usuário atualizado com sucesso.";
        }

        SucessoFormulario = true;
        Reload();
        await Task.CompletedTask;
    }

    // ---------------------------------------------------------------- Redefinir senha

    private string _novaSenhaRedefinir = string.Empty;
    public string NovaSenhaRedefinir
    {
        get => _novaSenhaRedefinir;
        set => SetProperty(ref _novaSenhaRedefinir, value);
    }

    private string _confirmarSenhaRedefinir = string.Empty;
    public string ConfirmarSenhaRedefinir
    {
        get => _confirmarSenhaRedefinir;
        set => SetProperty(ref _confirmarSenhaRedefinir, value);
    }

    private string? _mensagemRedefinir;
    public string? MensagemRedefinir
    {
        get => _mensagemRedefinir;
        set => SetProperty(ref _mensagemRedefinir, value);
    }

    private bool _sucessoRedefinir;
    public bool SucessoRedefinir
    {
        get => _sucessoRedefinir;
        set => SetProperty(ref _sucessoRedefinir, value);
    }

    private async Task RedefinirSenhaAsync()
    {
        SucessoRedefinir = false;

        if (SelectedUsuario is null)
        {
            MensagemRedefinir = "Selecione um usuário na lista.";
            return;
        }

        if (NovaSenhaRedefinir != ConfirmarSenhaRedefinir)
        {
            MensagemRedefinir = "A confirmação não corresponde à nova senha.";
            return;
        }

        AuthResult resultado = await _authService.RedefinirSenhaAsync(SelectedUsuario.Id, NovaSenhaRedefinir);
        SucessoRedefinir = resultado.Success;
        MensagemRedefinir = resultado.Success ? "Senha redefinida com sucesso." : resultado.ErrorMessage;

        if (resultado.Success)
        {
            NovaSenhaRedefinir = string.Empty;
            ConfirmarSenhaRedefinir = string.Empty;
        }
    }
}
