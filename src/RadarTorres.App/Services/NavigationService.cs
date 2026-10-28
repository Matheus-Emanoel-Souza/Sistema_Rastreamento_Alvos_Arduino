using System;
using System.Collections.Generic;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using RadarTorres.App.Models;
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
            [MenuItem.Desempenho] = () => _serviceProvider.GetRequiredService<DesempenhoView>(),
            [MenuItem.ObjetosDetectados] = () => _serviceProvider.GetRequiredService<ObjetosDetectadosView>(),
            [MenuItem.AcoesRealizadas] = () => _serviceProvider.GetRequiredService<AcoesRealizadasView>(),
            [MenuItem.HistoricoModos] = () => _serviceProvider.GetRequiredService<HistoricoModosView>(),
            [MenuItem.Usuarios] = () => _serviceProvider.GetRequiredService<UsuariosView>(),
            [MenuItem.Cameras] = () => _serviceProvider.GetRequiredService<CamerasView>(),
            [MenuItem.PerfilAlvo] = () => _serviceProvider.GetRequiredService<PerfilAlvoView>(),
            [MenuItem.ConfiguracoesArduino] = () => _serviceProvider.GetRequiredService<ArduinoSettingsView>(),
            [MenuItem.Configuracoes] = () => _serviceProvider.GetRequiredService<ConfiguracoesView>(),
            [MenuItem.TreinamentoControlador] = () => _serviceProvider.GetRequiredService<TreinamentoControladorView>(),
            [MenuItem.EstatisticasTreinamento] = () => _serviceProvider.GetRequiredService<EstatisticasTreinamentoView>(),
        };
    }

    /// <summary>Define o <see cref="ContentControl"/> que hospeda a view atual (chamado uma vez pela ShellWindow).</summary>
    public void SetHost(ContentControl host) => _host = host;

    /// <summary>
    /// Autorização de navegação no ÚNICO ponto por onde toda troca de tela passa. A Sidebar já
    /// esconde os itens que o perfil não pode ver (<see cref="ShellViewModel.RebuildMenu"/>) e o
    /// <see cref="ShellViewModel"/> repete a checagem antes de chamar este serviço, mas nenhuma
    /// das duas barreiras cobre os outros chamadores de <see cref="NavigateTo"/> —
    /// <c>PainelPrincipalViewModel</c> (atalhos do painel), <c>CamerasView</c> e
    /// <c>ShellView</c> injetam <see cref="INavigationService"/> e navegam diretamente. Com a
    /// regra aqui dentro, esconder o botão volta a ser só apresentação: a autorização de fato
    /// acontece neste método, para qualquer origem.
    ///
    /// As dependências são resolvidas sob demanda do <see cref="IServiceProvider"/>, e não pelo
    /// construtor, porque este serviço é criado na composição da raiz junto com os demais; pedir
    /// <see cref="IAuthService"/> no construtor acoplaria a ordem de registro no contêiner sem
    /// necessidade.
    /// </summary>
    private bool PodeNavegarPara(MenuItem item)
    {
        IAuthService auth = _serviceProvider.GetRequiredService<IAuthService>();
        IPermissionService permissoes = _serviceProvider.GetRequiredService<IPermissionService>();

        Usuario? usuario = auth.CurrentUser;
        if (usuario is null || !usuario.Ativo) return false;

        return permissoes.PodeVerMenu(usuario.Perfil, item);
    }

    public void NavigateTo(MenuItem item)
    {
        if (_host is null || !_factories.TryGetValue(item, out Func<object>? factory)) return;

        if (!PodeNavegarPara(item))
        {
            // Sem exceção: uma navegação negada não é erro de programação, é a regra funcionando.
            // Fica registrada para auditoria — sem nome de usuário, perfil ou qualquer dado de
            // sessão no log, apenas a tela recusada.
            _serviceProvider.GetRequiredService<ILoggingService>()
                .Warning($"Navegação bloqueada por falta de permissão ou sessão válida: {item}.");
            return;
        }

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
