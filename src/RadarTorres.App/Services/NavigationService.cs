using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RadarTorres.App.Views;
using RadarTorres.App.Views.Shell;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="INavigationService"/>. Diferente dos demais serviços do
/// projeto, referencia WPF diretamente (<see cref="ContentControl"/>) porque é
/// infraestrutura de UI (orquestra qual view aparece no conteúdo da Shell), não lógica de
/// negócio — a mesma separação "services não referenciam WPF" continua valendo para
/// Serial/Tracking/TowerSelection/FireControl/Simulation.
/// </summary>
public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<MenuItem, Func<object>> _factories;
    private ContentControl? _host;

    public MenuItem? CurrentItem { get; private set; }

    public event EventHandler<MenuItem>? Navigated;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _factories = new Dictionary<MenuItem, Func<object>>
        {
            [MenuItem.PainelPrincipal] = () => _serviceProvider.GetRequiredService<PainelPrincipalView>(),
            [MenuItem.Monitoramento] = () => _serviceProvider.GetRequiredService<MonitoramentoView>(),
            [MenuItem.ObjetosDetectados] = () => new PlaceholderView("Sidebar.ObjetosDetectados"),
            [MenuItem.AcoesRealizadas] = () => new PlaceholderView("Sidebar.AcoesRealizadas"),
            [MenuItem.HistoricoModos] = () => new PlaceholderView("Sidebar.HistoricoModos"),
            [MenuItem.Usuarios] = () => new PlaceholderView("Sidebar.Usuarios"),
            [MenuItem.ChamadosAjuda] = () => new PlaceholderView("Sidebar.ChamadosAjuda"),
            [MenuItem.Configuracoes] = () => new PlaceholderView("Sidebar.Configuracoes"),
            [MenuItem.ConfiguracoesArduino] = () => _serviceProvider.GetRequiredService<ArduinoSettingsView>(),
        };
    }

    /// <summary>Define o <see cref="ContentControl"/> que hospeda a view atual (chamado uma vez pela ShellWindow).</summary>
    public void SetHost(ContentControl host) => _host = host;

    public void NavigateTo(MenuItem item)
    {
        if (_host is null || !_factories.TryGetValue(item, out Func<object>? factory)) return;

        object view = factory();
        _host.Content = view;
        CurrentItem = item;

        if (view is INavigationAware aware)
        {
            aware.OnNavigatedTo();
        }

        Navigated?.Invoke(this, item);
    }
}
