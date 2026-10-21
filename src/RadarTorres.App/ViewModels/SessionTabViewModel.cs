using Microsoft.Extensions.DependencyInjection;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// Representa uma aba do <c>MainWindow</c> (Requisito "abas como em navegador"): cada aba é uma
/// sessão/login independente (<see cref="IServiceScope"/> próprio), com o conteúdo trocável
/// entre a tela de login (<c>LoginView</c>) e o shell pós-login (<c>ShellView</c>) — ver
/// <c>App.OpenNewSession</c>/<c>StartLoginFlow</c>/<c>ShowShellInTab</c>. Criada e destruída
/// inteiramente por <c>App.xaml.cs</c>, nunca resolvida via DI.
/// </summary>
public sealed class SessionTabViewModel : ViewModelBase
{
    /// <summary>Escopo de DI da sessão atual desta aba. Trocado (não recriado) quando o usuário
    /// faz logout — a mesma aba volta para a tela de login com um escopo novo.</summary>
    public IServiceScope Session { get; set; }

    private string _header = string.Empty;
    public string Header
    {
        get => _header;
        set => SetProperty(ref _header, value);
    }

    /// <summary>Conteúdo atual da aba: uma <c>LoginView</c> (ainda não autenticado) ou uma
    /// <c>ShellView</c> (pós-login).</summary>
    private object? _content;
    public object? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    /// <summary>Mantido por <c>MainWindow.xaml.cs</c> — só a aba igual a <c>SelectedTab</c> fica
    /// com <c>true</c>, usado apenas para destacar visualmente a aba ativa na faixa de abas.</summary>
    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public SessionTabViewModel(IServiceScope session)
    {
        Session = session;
    }
}
