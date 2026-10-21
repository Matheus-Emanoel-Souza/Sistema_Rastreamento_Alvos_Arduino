using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using RadarTorres.App.Configuration;
using RadarTorres.App.Data;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>Opção de tamanho pronto oferecida na tela de Configurações.</summary>
/// <param name="Rotulo">Texto exibido no seletor.</param>
public sealed record TamanhoJanelaPredefinido(string Rotulo, double Largura, double Altura)
{
    public override string ToString() => Rotulo;
}

/// <summary>
/// ViewModel da tela "Configurações": edita as <see cref="ConfiguracoesAplicativo"/> desta
/// máquina e as grava em disco, para a próxima execução já abrir nas condições escolhidas.
/// </summary>
/// <remarks>
/// Trabalha sobre uma CÓPIA das configurações (<see cref="ConfiguracoesAplicativo.Clonar"/>) e
/// só grava no "Salvar": assim "Cancelar" tem o que desfazer e mexer em um controle não altera
/// o comportamento do aplicativo em uso antes de o usuário confirmar.
/// </remarks>
public sealed class ConfiguracoesViewModel : ViewModelBase
{
    private readonly IConfiguracoesAplicativoRepository _repositorio;
    private readonly ILocalizationService _localizationService;
    private readonly IThemeService _themeService;
    private readonly ITermosDeUsoService _termosDeUsoService;
    private readonly ILoggingService _logger;

    private ConfiguracoesAplicativo _edicao;

    public ConfiguracoesViewModel(
        IConfiguracoesAplicativoRepository repositorio,
        ILocalizationService localizationService,
        IThemeService themeService,
        ITermosDeUsoService termosDeUsoService,
        ILoggingService logger)
    {
        _repositorio = repositorio;
        _localizationService = localizationService;
        _themeService = themeService;
        _termosDeUsoService = termosDeUsoService;
        _logger = logger;

        _edicao = repositorio.Atual.Clonar();

        SalvarCommand = new RelayCommand(_ => Salvar());
        RestaurarPadroesCommand = new RelayCommand(_ => RestaurarPadroes());
        CancelarCommand = new RelayCommand(_ => Cancelar());
        AbrirPastaDiagnosticoCommand = new RelayCommand(_ => AbrirPastaDiagnostico());
        AbrirTermosDeUsoCommand = new RelayCommand(_ => AbrirTermosDeUsoSolicitado?.Invoke(this, EventArgs.Empty));
        BaixarTermosDeUsoCommand = new RelayCommand(_ => BaixarTermosDeUsoSolicitado?.Invoke(this, _termosDeUsoService.NomeArquivoSugerido));
        UsarTamanhoAtualCommand = new RelayCommand(_ => UsarTamanhoAtualDaJanela());
    }

    // ---------------------------------------------------------------- Opções fixas

    public IReadOnlyList<ModoAberturaJanela> ModosAbertura { get; } =
        [ModoAberturaJanela.TamanhoPersonalizado, ModoAberturaJanela.Maximizada, ModoAberturaJanela.LembrarUltimoTamanho];

    /// <summary>
    /// Tamanhos prontos cobrindo as resoluções mais comuns. Existem para o usuário não precisar
    /// adivinhar números que caibam na tela dele — o campo livre continua disponível ao lado.
    /// </summary>
    public IReadOnlyList<TamanhoJanelaPredefinido> TamanhosPredefinidos { get; } =
    [
        new("1280 x 720 (HD)", 1280, 720),
        new("1366 x 768 (notebook comum)", 1366, 768),
        new("1450 x 860 (padrão do sistema)", 1450, 860),
        new("1600 x 900", 1600, 900),
        new("1920 x 1080 (Full HD)", 1920, 1080)
    ];

    public IReadOnlyList<string> IdiomasDisponiveis => _localizationService.AvailableLanguages;

    public IReadOnlyList<TemaPreferido> TemasDisponiveis { get; } =
        [TemaPreferido.Escuro, TemaPreferido.Claro, TemaPreferido.Sistema];

    // ---------------------------------------------------------------- Propriedades editáveis

    public ModoAberturaJanela ModoAbertura
    {
        get => _edicao.ModoAbertura;
        set
        {
            if (_edicao.ModoAbertura == value) return;
            _edicao.ModoAbertura = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(TamanhoPersonalizadoHabilitado));
            MarcarAlterado();
        }
    }

    /// <summary>Os campos de largura/altura só fazem sentido no modo de tamanho personalizado.</summary>
    public bool TamanhoPersonalizadoHabilitado => ModoAbertura == ModoAberturaJanela.TamanhoPersonalizado;

    public double LarguraJanela
    {
        get => _edicao.LarguraJanela;
        set
        {
            // Limites alinhados ao MinWidth/MinHeight da própria janela: abaixo disso o WPF
            // ignoraria o valor e a configuração salva não corresponderia ao que acontece.
            double ajustado = Math.Clamp(value, LarguraMinima, LarguraMaxima);
            if (Math.Abs(_edicao.LarguraJanela - ajustado) < 0.5) return;

            _edicao.LarguraJanela = ajustado;
            OnPropertyChanged();
            MarcarAlterado();
        }
    }

    public double AlturaJanela
    {
        get => _edicao.AlturaJanela;
        set
        {
            double ajustado = Math.Clamp(value, AlturaMinima, AlturaMaxima);
            if (Math.Abs(_edicao.AlturaJanela - ajustado) < 0.5) return;

            _edicao.AlturaJanela = ajustado;
            OnPropertyChanged();
            MarcarAlterado();
        }
    }

    public const double LarguraMinima = 1100;
    public const double AlturaMinima = 650;
    public const double LarguraMaxima = 7680;
    public const double AlturaMaxima = 4320;

    private TamanhoJanelaPredefinido? _tamanhoSelecionado;
    public TamanhoJanelaPredefinido? TamanhoSelecionado
    {
        get => _tamanhoSelecionado;
        set
        {
            if (!SetProperty(ref _tamanhoSelecionado, value) || value is null) return;

            LarguraJanela = value.Largura;
            AlturaJanela = value.Altura;
        }
    }

    public string Idioma
    {
        get => _edicao.Idioma;
        set
        {
            if (string.Equals(_edicao.Idioma, value, StringComparison.OrdinalIgnoreCase)) return;
            _edicao.Idioma = value;
            OnPropertyChanged();
            MarcarAlterado();
        }
    }

    public TemaPreferido Tema
    {
        get => _edicao.Tema;
        set
        {
            if (_edicao.Tema == value) return;
            _edicao.Tema = value;
            OnPropertyChanged();
            MarcarAlterado();
        }
    }

    public bool IniciarCameraAutomaticamente
    {
        get => _edicao.IniciarCameraAutomaticamente;
        set
        {
            if (_edicao.IniciarCameraAutomaticamente == value) return;
            _edicao.IniciarCameraAutomaticamente = value;
            OnPropertyChanged();
            MarcarAlterado();
        }
    }

    public bool MostrarCaixasDeteccao
    {
        get => _edicao.MostrarCaixasDeteccao;
        set
        {
            if (_edicao.MostrarCaixasDeteccao == value) return;
            _edicao.MostrarCaixasDeteccao = value;
            OnPropertyChanged();
            MarcarAlterado();
        }
    }

    public bool AlertaSonoroHabilitado
    {
        get => _edicao.AlertaSonoroHabilitado;
        set
        {
            if (_edicao.AlertaSonoroHabilitado == value) return;
            _edicao.AlertaSonoroHabilitado = value;
            OnPropertyChanged();
            MarcarAlterado();
        }
    }

    public bool GerarRelatorioDesempenho
    {
        get => _edicao.GerarRelatorioDesempenho;
        set
        {
            if (_edicao.GerarRelatorioDesempenho == value) return;
            _edicao.GerarRelatorioDesempenho = value;
            OnPropertyChanged();
            MarcarAlterado();
        }
    }

    // ---------------------------------------------------------------- Estado da tela

    private bool _temAlteracoesNaoSalvas;
    public bool TemAlteracoesNaoSalvas
    {
        get => _temAlteracoesNaoSalvas;
        private set => SetProperty(ref _temAlteracoesNaoSalvas, value);
    }

    private string? _mensagemStatus;
    public string? MensagemStatus
    {
        get => _mensagemStatus;
        private set => SetProperty(ref _mensagemStatus, value);
    }

    public string CaminhoArquivoConfiguracoes => _repositorio.CaminhoArquivo;

    public string PastaRelatorios => AppDataPaths.DiagnosticoFolder;

    // ---------------------------------------------------------------- Comandos

    public RelayCommand SalvarCommand { get; }
    public RelayCommand RestaurarPadroesCommand { get; }
    public RelayCommand CancelarCommand { get; }
    public RelayCommand AbrirPastaDiagnosticoCommand { get; }
    public RelayCommand UsarTamanhoAtualCommand { get; }

    // ---------------------------------------------------------------- Termos de Uso
    //
    // A tela só dá acesso ao documento; quem o lê e o converte em PDF é o ITermosDeUsoService,
    // a partir do mesmo arquivo exibido pelo instalador. Abrir janela e mostrar diálogo de
    // arquivo são responsabilidade da View (ver ConfiguracoesView.xaml.cs), como no restante do
    // projeto.

    /// <summary>Abre a janela de leitura dos termos.</summary>
    public RelayCommand AbrirTermosDeUsoCommand { get; }

    /// <summary>Exporta o documento integral em PDF, sem precisar abrir a janela antes.</summary>
    public RelayCommand BaixarTermosDeUsoCommand { get; }

    public event EventHandler? AbrirTermosDeUsoSolicitado;

    public event EventHandler<string>? BaixarTermosDeUsoSolicitado;

    /// <summary>Linha de vigência do documento aceito nesta instalação (ex.: "Versão 1.1 — vigente a partir de …").</summary>
    public string VersaoTermosDeUso => _termosDeUsoService.Obter().Versao;

    private void MarcarAlterado()
    {
        TemAlteracoesNaoSalvas = true;
        MensagemStatus = null;
    }

    private void Salvar()
    {
        _repositorio.Salvar(_edicao);

        // Idioma e tema são aplicados NA HORA (os demais ajustes valem na próxima abertura):
        // são mudanças visíveis imediatamente, e esperar um reinício para ver o idioma trocar
        // passaria a impressão de que o botão não funcionou.
        _localizationService.SetLanguage(_edicao.Idioma);
        _themeService.ApplyTheme(_edicao.Tema);

        _edicao = _repositorio.Atual.Clonar();

        TemAlteracoesNaoSalvas = false;
        MensagemStatus = ModoAbertura == ModoAberturaJanela.Maximizada || TamanhoPersonalizadoHabilitado
            ? "Configurações salvas. O tamanho da janela será aplicado na próxima vez que o aplicativo abrir."
            : "Configurações salvas.";

        _logger.Success($"Configurações do aplicativo salvas em {_repositorio.CaminhoArquivo}.");
    }

    private void RestaurarPadroes()
    {
        _edicao = ConfiguracoesAplicativo.Padrao();
        _tamanhoSelecionado = null;

        NotificarTudo();

        TemAlteracoesNaoSalvas = true;
        MensagemStatus = "Padrões restaurados na tela. Clique em \"Salvar\" para confirmar.";
    }

    private void Cancelar()
    {
        _edicao = _repositorio.Atual.Clonar();
        _tamanhoSelecionado = null;

        NotificarTudo();

        TemAlteracoesNaoSalvas = false;
        MensagemStatus = "Alterações descartadas.";
    }

    /// <summary>Copia para os campos o tamanho em que a janela está agora — mais prático que medir na mão.</summary>
    private void UsarTamanhoAtualDaJanela()
    {
        System.Windows.Window? janela = System.Windows.Application.Current?.MainWindow;
        if (janela is null) return;

        ModoAbertura = ModoAberturaJanela.TamanhoPersonalizado;
        LarguraJanela = janela.ActualWidth;
        AlturaJanela = janela.ActualHeight;

        MensagemStatus = $"Tamanho atual da janela copiado ({janela.ActualWidth:0} x {janela.ActualHeight:0}).";
    }

    private void AbrirPastaDiagnostico()
    {
        try
        {
            Directory.CreateDirectory(AppDataPaths.DiagnosticoFolder);
            Process.Start(new ProcessStartInfo(AppDataPaths.DiagnosticoFolder) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MensagemStatus = $"Não foi possível abrir a pasta: {ex.Message}";
        }
    }

    private void NotificarTudo()
    {
        OnPropertyChanged(nameof(ModoAbertura));
        OnPropertyChanged(nameof(TamanhoPersonalizadoHabilitado));
        OnPropertyChanged(nameof(LarguraJanela));
        OnPropertyChanged(nameof(AlturaJanela));
        OnPropertyChanged(nameof(TamanhoSelecionado));
        OnPropertyChanged(nameof(Idioma));
        OnPropertyChanged(nameof(Tema));
        OnPropertyChanged(nameof(IniciarCameraAutomaticamente));
        OnPropertyChanged(nameof(MostrarCaixasDeteccao));
        OnPropertyChanged(nameof(AlertaSonoroHabilitado));
        OnPropertyChanged(nameof(GerarRelatorioDesempenho));
    }
}
