using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views.Shell;

/// <summary>
/// Única janela do Windows do aplicativo (Requisito "abas como em navegador"): antes cada
/// login/sessão abria em uma janela própria (<c>LoginWindow</c> -&gt; <c>ShellWindow</c>); agora
/// tudo mora aqui dentro, em abas (<see cref="SessionTabViewModel"/>) — "Nova Página" (ver
/// <c>TopBarView</c>/<c>ShellViewModel.NovaJanelaCommand</c>) e o botão "+" desta janela abrem
/// uma aba nova, não outra janela do sistema operacional. Orquestração de sessão (abrir/fechar
/// abas, trocar login-&gt;shell) continua em <c>App.xaml.cs</c> — esta classe só expõe
/// <see cref="Tabs"/>/<see cref="SelectedTab"/> e os cliques da faixa de abas.
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    public ObservableCollection<SessionTabViewModel> Tabs { get; } = new();

    private SessionTabViewModel? _selectedTab;
    public SessionTabViewModel? SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (ReferenceEquals(_selectedTab, value)) return;
            _selectedTab = value;
            foreach (SessionTabViewModel tab in Tabs)
            {
                tab.IsSelected = ReferenceEquals(tab, value);
            }
            OnPropertyChanged();
        }
    }

    /// <summary>Disparado ao clicar no botão "+" da faixa de abas — <c>App.xaml.cs</c> abre uma
    /// nova sessão/aba (ver <c>App.OpenNewSession</c>).</summary>
    public event EventHandler? NovaAbaSolicitada;

    /// <summary>Disparado ao clicar no "✕" de uma aba — <c>App.xaml.cs</c> encerra a sessão dessa
    /// aba e a remove (ver <c>App.FecharAba</c>).</summary>
    public event EventHandler<SessionTabViewModel>? AbaFechada;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void TabHeader_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is SessionTabViewModel tab)
        {
            SelectedTab = tab;
        }
    }

    private void TabClose_Click(object sender, RoutedEventArgs e)
    {
        // Não deixa o clique "vazar" para o botão da aba por baixo (que selecionaria a aba
        // sendo fechada logo antes dela sumir).
        e.Handled = true;

        if (((FrameworkElement)sender).Tag is SessionTabViewModel tab)
        {
            AbaFechada?.Invoke(this, tab);
        }
    }

    private void NewTab_Click(object sender, RoutedEventArgs e) => NovaAbaSolicitada?.Invoke(this, EventArgs.Empty);
}
