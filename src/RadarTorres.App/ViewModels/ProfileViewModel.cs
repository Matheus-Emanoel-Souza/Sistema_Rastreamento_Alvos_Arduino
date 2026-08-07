using System.Threading.Tasks;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel da janela de perfil: dados básicos do usuário + alteração segura de senha
/// (Requisito 7 — "Alteração segura de senha"). Senhas ficam só em memória durante a
/// operação, nunca logadas.
/// </summary>
public sealed class ProfileViewModel : ViewModelBase
{
    private readonly IAuthService _authService;

    public ProfileViewModel(IAuthService authService)
    {
        _authService = authService;
        AlterarSenhaCommand = new RelayCommand(async () => await AlterarSenhaAsync());
    }

    public Usuario? Usuario => _authService.CurrentUser;

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
