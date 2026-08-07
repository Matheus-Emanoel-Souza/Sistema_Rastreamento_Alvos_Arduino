using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;

namespace RadarTorres.App.Views;

/// <summary>
/// Code-behind da janela principal. Atua como "composition root" simplificado: instancia os
/// serviços concretos e monta a <see cref="MainViewModel"/> — o projeto não usa um container de
/// injeção de dependência completo por ser de porte pequeno/médio (ver comentário em AppConfig).
/// Não contém nenhuma lógica de negócio: apenas ligações de interface (ex.: clique no radar,
/// auto-scroll do console de eventos).
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        ILoggingService logger = new LoggingService(Dispatcher);
        ISerialCommunicationService serialService = new SerialCommunicationService(logger);
        ITargetTrackingService trackingService = new TargetTrackingService(logger, Dispatcher);
        ITowerSelectionService towerService = new TowerSelectionService(logger);
        IFireControlService fireControlService = new FireControlService(logger, Dispatcher);
        ISimulationService simulationService = new SimulationService(logger);

        _viewModel = new MainViewModel(logger, serialService, trackingService, towerService, fireControlService, simulationService);
        DataContext = _viewModel;

        _viewModel.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
        Closing += (_, _) => _viewModel.Dispose();
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
