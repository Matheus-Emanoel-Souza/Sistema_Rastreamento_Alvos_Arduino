using System.Collections.Specialized;
using System.Windows.Controls;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da tela de monitoramento (radar, conexão serial, controle do sistema —
/// conteúdo original de MainWindow, hoje hospedado pela ShellWindow via barra lateral).
/// A <see cref="MainViewModel"/> é injetada via DI (Singleton — ver App.xaml.cs) em vez de
/// ser montada manualmente aqui, mas nenhum comportamento mudou: continua sem lógica de
/// negócio, só ligações de interface (clique no radar, auto-scroll do console de eventos).
/// </summary>
public partial class MonitoramentoView : UserControl
{
    private readonly MainViewModel _viewModel;

    public MonitoramentoView(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = _viewModel;

        _viewModel.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
    }

    private void Radar_TargetClicked(object? sender, int targetId) => _viewModel.SelectTargetById(targetId);

    private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add) return;

        if (FindName("EventLogListBox") is ListBox listBox && listBox.Items.Count > 0)
        {
            listBox.ScrollIntoView(listBox.Items[^1]);
        }
    }
}
