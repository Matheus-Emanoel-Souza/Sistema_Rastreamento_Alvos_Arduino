using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using RadarTorres.App.Configuration;

namespace RadarTorres.App.Services;

/// <summary>
/// Leitura e gravação das <see cref="ConfiguracoesAplicativo"/> desta máquina.
/// </summary>
public interface IConfiguracoesAplicativoRepository
{
    /// <summary>
    /// Configurações em vigor. Carregadas do disco na primeira chamada e mantidas em memória —
    /// são lidas em pontos sensíveis a latência (abertura da janela, cada quadro da detecção),
    /// onde reler o arquivo seria desperdício.
    /// </summary>
    ConfiguracoesAplicativo Atual { get; }

    /// <summary>Grava as configurações informadas e passa a considerá-las as atuais.</summary>
    void Salvar(ConfiguracoesAplicativo configuracoes);

    /// <summary>Caminho do arquivo em disco, exibido na tela de Configurações.</summary>
    string CaminhoArquivo { get; }
}

/// <summary>
/// Implementação em JSON, gravada em <c>%LocalAppData%\RadarTorres\configuracoes-app.json</c> —
/// pasta gravável por usuário comum, nunca dentro da pasta de instalação (que exige
/// administrador), mesmo critério já usado por <see cref="ArduinoSettingsRepository"/>.
/// </summary>
public sealed class ConfiguracoesAplicativoRepository : IConfiguracoesAplicativoRepository
{
    /// <summary>
    /// Bridge estático para código instanciado via XAML sem DI (ver <see cref="Views.RadarControl"/>
    /// e o mesmo padrão em <see cref="LocalizationService.Current"/>/<see cref="DesempenhoMonitorService.Current"/>)
    /// — atribuído em <c>App.OnStartup</c> depois do container pronto.
    /// </summary>
    public static IConfiguracoesAplicativoRepository? Current { get; set; }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly ILoggingService? _logger;
    private ConfiguracoesAplicativo? _atual;

    public string CaminhoArquivo { get; }

    public ConfiguracoesAplicativoRepository(ILoggingService? logger = null)
        : this(CaminhoPadrao(), logger)
    {
    }

    public ConfiguracoesAplicativoRepository(string caminhoArquivo, ILoggingService? logger = null)
    {
        CaminhoArquivo = caminhoArquivo;
        _logger = logger;
    }

    public static string CaminhoPadrao() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RadarTorres",
        "configuracoes-app.json");

    public ConfiguracoesAplicativo Atual => _atual ??= Carregar();

    private ConfiguracoesAplicativo Carregar()
    {
        try
        {
            if (!File.Exists(CaminhoArquivo))
            {
                // Primeira execução nesta conta de usuário: parte dos padrões, mas respeitando
                // o idioma que foi escolhido durante a instalação.
                var novas = ConfiguracoesAplicativo.Padrao();
                novas.Idioma = IdiomaInicialInstalado() ?? novas.Idioma;
                return novas;
            }

            string json = File.ReadAllText(CaminhoArquivo);
            ConfiguracoesAplicativo configuracoes = JsonSerializer.Deserialize<ConfiguracoesAplicativo>(json, SerializerOptions)
                ?? ConfiguracoesAplicativo.Padrao();
            configuracoes.LimiarConfiancaDisparoPercentual = Math.Max(
                ConfiguracoesAplicativo.ConfiancaMinimaDisparoPercentual, configuracoes.LimiarConfiancaDisparoPercentual);
            configuracoes.AlcanceMiraMetros = Math.Clamp(
                configuracoes.AlcanceMiraMetros, ConfiguracoesAplicativo.AlcanceMiraMinimoMetros, ConfiguracoesAplicativo.AlcanceMiraMaximoMetros);
            configuracoes.VelocidadeRapidaMetrosPorSegundo = Math.Clamp(
                configuracoes.VelocidadeRapidaMetrosPorSegundo, ConfiguracoesAplicativo.VelocidadeRapidaMinimaMetrosPorSegundo, ConfiguracoesAplicativo.VelocidadeRapidaMaximaMetrosPorSegundo);
            return configuracoes;
        }
        catch (Exception ex)
        {
            // Arquivo corrompido não pode impedir o aplicativo de abrir — segue com os padrões,
            // mesmo tratamento já dado a appsettings.json em App.LoadAppSettings.
            _logger?.Warning($"Não foi possível ler as configurações do aplicativo ({CaminhoArquivo}): {ex.Message}. Usando valores padrão.");
            return ConfiguracoesAplicativo.Padrao();
        }
    }

    /// <summary>
    /// Idioma escolhido na tela de idioma do instalador, gravado por ele em
    /// <c>idioma-padrao.txt</c> dentro da pasta de instalação.
    /// </summary>
    /// <remarks>
    /// O instalador roda como administrador e não consegue gravar na pasta de preferências do
    /// usuário final (apontaria para o perfil do administrador), então deixa a escolha num
    /// arquivo da pasta de instalação, legível por todos. Cada conta de usuário lê daqui uma
    /// única vez, na primeira execução; depois a preferência vive no próprio JSON desta conta e
    /// este arquivo deixa de ser consultado.
    /// </remarks>
    private string? IdiomaInicialInstalado()
    {
        try
        {
            string caminho = Path.Combine(AppContext.BaseDirectory, "idioma-padrao.txt");
            if (!File.Exists(caminho)) return null;

            string idioma = File.ReadAllText(caminho).Trim();
            return idioma is "pt-BR" or "en-US" ? idioma : null;
        }
        catch (Exception)
        {
            // Execução fora de uma instalação (desenvolvimento) ou arquivo ilegível: o padrão
            // do próprio modelo resolve, não há o que registrar.
            return null;
        }
    }

    public void Salvar(ConfiguracoesAplicativo configuracoes)
    {
        configuracoes.LimiarConfiancaDisparoPercentual = Math.Max(
            ConfiguracoesAplicativo.ConfiancaMinimaDisparoPercentual, configuracoes.LimiarConfiancaDisparoPercentual);
        configuracoes.AlcanceMiraMetros = Math.Clamp(
            configuracoes.AlcanceMiraMetros, ConfiguracoesAplicativo.AlcanceMiraMinimoMetros, ConfiguracoesAplicativo.AlcanceMiraMaximoMetros);
        configuracoes.VelocidadeRapidaMetrosPorSegundo = Math.Clamp(
            configuracoes.VelocidadeRapidaMetrosPorSegundo, ConfiguracoesAplicativo.VelocidadeRapidaMinimaMetrosPorSegundo, ConfiguracoesAplicativo.VelocidadeRapidaMaximaMetrosPorSegundo);
        _atual = configuracoes;

        try
        {
            string? pasta = Path.GetDirectoryName(CaminhoArquivo);
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            File.WriteAllText(CaminhoArquivo, JsonSerializer.Serialize(configuracoes, SerializerOptions));
        }
        catch (Exception ex)
        {
            _logger?.Error($"Não foi possível salvar as configurações do aplicativo: {ex.Message}");
        }
    }
}
