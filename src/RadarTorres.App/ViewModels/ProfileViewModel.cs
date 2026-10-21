using System.Globalization;
using System.Threading.Tasks;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da janela de perfil: dados básicos do usuário + alteração segura de senha
/// (Requisito 7 — "Alteração segura de senha"). Um usuário só edita os PRÓPRIOS dados aqui, e
/// só nome/e-mail/idade — login, função (perfil) e ativo/inativo são exclusivos do
/// Administrador na tela "Usuários" (ver <see cref="UsuariosViewModel"/>). Senhas ficam só em
/// memória durante a operação, nunca logadas.
/// </summary>
public sealed class ProfileViewModel : ViewModelBase
{
    private readonly IAuthService _authService;
    private readonly IUsuarioRepository _usuarioRepository;

    public ProfileViewModel(IAuthService authService, IUsuarioRepository usuarioRepository)
    {
        _authService = authService;
        _usuarioRepository = usuarioRepository;

        AlterarSenhaCommand = new RelayCommand(async () => await AlterarSenhaAsync());
        SalvarDadosCommand = new RelayCommand(SalvarDados);

        CarregarDados();
    }

    public Usuario? Usuario => _authService.CurrentUser;

    private string _nome = string.Empty;
    public string Nome
    {
        get => _nome;
        set => SetProperty(ref _nome, value);
    }

    private string _email = string.Empty;
    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    private string _idade = string.Empty;
    public string Idade
    {
        get => _idade;
        set => SetProperty(ref _idade, value);
    }

    private string? _mensagemDados;
    public string? MensagemDados
    {
        get => _mensagemDados;
        set => SetProperty(ref _mensagemDados, value);
    }

    private bool _sucessoDados;
    public bool SucessoDados
    {
        get => _sucessoDados;
        set => SetProperty(ref _sucessoDados, value);
    }

    public RelayCommand SalvarDadosCommand { get; }

    private void CarregarDados()
    {
        if (Usuario is null) return;

        Nome = Usuario.Nome;
        Email = Usuario.Email;
        Idade = Usuario.Idade?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private void SalvarDados()
    {
        SucessoDados = false;

        if (Usuario is null)
        {
            MensagemDados = "Nenhum usuário conectado.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Nome))
        {
            MensagemDados = "Informe seu nome.";
            return;
        }

        int? idade = null;
        if (!string.IsNullOrWhiteSpace(Idade))
        {
            if (!int.TryParse(Idade, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idadeValor) || idadeValor < 0)
            {
                MensagemDados = "Idade inválida.";
                return;
            }
            idade = idadeValor;
        }

        Usuario.Nome = Nome.Trim();
        Usuario.Email = Email.Trim();
        Usuario.Idade = idade;
        _usuarioRepository.Update(Usuario);

        OnPropertyChanged(nameof(Usuario));
        SucessoDados = true;
        MensagemDados = "Dados atualizados com sucesso.";
    }

    private string _senhaAtual = string.Empty;
    public string SenhaAtual
    {
        get => _senhaAtual;
        set => SetProperty(ref _senhaAtual, value);
    }

    private string _novaSenha = string.Empty;
    public string NovaSenha
    {
        get => _novaSenha;
        set => SetProperty(ref _novaSenha, value);
    }

    private string _confirmarNovaSenha = string.Empty;
    public string ConfirmarNovaSenha
    {
        get => _confirmarNovaSenha;
        set => SetProperty(ref _confirmarNovaSenha, value);
    }

    private string? _mensagem;
    public string? Mensagem
    {
        get => _mensagem;
        set => SetProperty(ref _mensagem, value);
    }

    private bool _sucesso;
    public bool Sucesso
    {
        get => _sucesso;
        set => SetProperty(ref _sucesso, value);
    }

    public RelayCommand AlterarSenhaCommand { get; }

    private async Task AlterarSenhaAsync()
    {
        Sucesso = false;

        if (NovaSenha != ConfirmarNovaSenha)
        {
            Mensagem = "A confirmação não corresponde à nova senha.";
            return;
        }

        AuthResult resultado = await _authService.AlterarSenhaAsync(SenhaAtual, NovaSenha);
        Sucesso = resultado.Success;
        Mensagem = resultado.Success ? "Senha alterada com sucesso." : resultado.ErrorMessage;

        if (resultado.Success)
        {
            SenhaAtual = string.Empty;
            NovaSenha = string.Empty;
            ConfirmarNovaSenha = string.Empty;
        }
    }
}
