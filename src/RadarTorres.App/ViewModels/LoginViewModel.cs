using System;
using System.Threading.Tasks;
using RadarTorres.App.Helpers;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>ViewModel da tela de login. Nenhuma senha é mantida em log — só em memória, durante a tentativa.</summary>
public sealed class LoginViewModel : ViewModelBase
{
    private readonly IAuthService _authService;

    public LoginViewModel(IAuthService authService)
    {
        _authService = authService;
        LoginCommand = new RelayCommand(async () => await LoginAsync(), () => !IsBusy);
    }

    private string _login = string.Empty;
    public string Login
    {
        get => _login;
        set => SetProperty(ref _login, value);
    }

    /// <summary>
    /// Preenchida pelo code-behind a partir do <c>PasswordBox</c> (que não suporta binding
    /// direto de <c>Password</c> por questão de segurança do próprio WPF).
    /// </summary>
    public string Senha { get; set; } = string.Empty;

    private string? _errorMessage;
    public string? ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public RelayCommand LoginCommand { get; }

    /// <summary>Disparado quando o login é bem-sucedido — o code-behind fecha a janela ao ouvir este evento.</summary>
    public event EventHandler? LoginSucceeded;

    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            AuthResult result = await _authService.LoginAsync(Login, Senha);
            if (result.Success)
            {
                LoginSucceeded?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                ErrorMessage = result.ErrorMessage;
            }
        }
        finally
        {
            IsBusy = false;
        }
    }
}
