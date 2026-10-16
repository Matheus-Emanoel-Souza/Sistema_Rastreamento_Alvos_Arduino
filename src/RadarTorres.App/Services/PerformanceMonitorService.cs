using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using RadarTorres.App.Configuration;
using RadarTorres.App.Data;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IPerformanceMonitor"/>: um arquivo <c>.log</c> legível por
/// execução em <see cref="AppDataPaths.DiagnosticoFolder"/>.
/// </summary>
/// <remarks>
/// <para>
/// A ordem de escrita é deliberada. Ambiente, recursos e parâmetros saem IMEDIATAMENTE na
/// construção; eventos e erros são anexados conforme acontecem; só as estatísticas agregadas
/// ficam para o encerramento. Assim, se o aplicativo morrer de forma anormal — justamente
/// quando o relatório mais importa — o arquivo ainda contém a máquina, a configuração em vigor
/// e o último erro registrado, em vez de ficar vazio.
/// </para>
/// <para>
/// As rodadas de detecção são acumuladas em memória e resumidas no fim, não escritas uma a uma:
/// a detecção roda ~8 vezes por segundo, então meia hora de uso daria mais de 14 mil linhas.
/// O que responde "o sistema está rápido o bastante?" é a distribuição (mediana, p95, pior
/// caso) e onde o tempo é gasto, não a rodada individual.
/// </para>
/// </remarks>
public sealed class PerformanceMonitorService : IPerformanceMonitor
{
    /// <summary>Teto de amostras de duração guardadas para calcular percentis (~10h de sessão).</summary>
    private const int MaxAmostras = 300_000;

    /// <summary>Teto de linhas da linha do tempo, para uma sessão longa não inflar o arquivo.</summary>
    private const int MaxEventos = 500;

    /// <summary>Teto de erros detalhados. Acima disso só o contador continua subindo.</summary>
    private const int MaxErros = 200;

    private static readonly CultureInfo Cultura = new("pt-BR");

    private readonly ILoggingService _logger;
    private readonly object _trava = new();

    private readonly StreamWriter? _escritor;
    private readonly DateTime _inicio = DateTime.Now;

    private readonly List<double> _duracoes = new();
    private readonly List<double> _latenciasAquisicao = new();
    private readonly List<double> _pontuacoesRejeitadas = new();

    private int _rodadas;
    private int _rodadasComDetector;
    private int _rodadasIdentificadas;
    private int _eventos;
    private int _erros;

    private double _somaPreparoMs;
    private double _somaReferenciaMs;
    private double _somaSegmentacaoMs;
    private double _somaDetectorMs;
    private double _somaComparacaoMs;

    private string _resolucaoQuadro = "(câmera não utilizada)";
    private string _perfilMaisRecente = "(nenhum)";
    private bool _finalizado;

    public string? CaminhoArquivo { get; }

    public PerformanceMonitorService(ILoggingService logger, IConfiguracoesAplicativoRepository configuracoes)
    {
        _logger = logger;

        try
        {
            if (!configuracoes.Atual.GerarRelatorioDesempenho)
            {
                // Desligado na tela de Configurações: o serviço continua existindo e sendo
                // chamado normalmente, mas sem escritor todos os métodos viram no-op. Mais
                // simples e seguro que espalhar verificações de "está habilitado?" por quem
                // chama — e permite religar sem mexer em nenhum ponto de chamada.
                _escritor = null;
                CaminhoArquivo = null;
                return;
            }

            AppDataPaths.EnsureDiagnosticoFolderExists();

            CaminhoArquivo = Path.Combine(
                AppDataPaths.DiagnosticoFolder,
                $"desempenho-{_inicio:yyyyMMdd-HHmmss}.log");

            _escritor = new StreamWriter(CaminhoArquivo, append: false, Encoding.UTF8) { AutoFlush = true };

            EscreverCabecalho();
        }
        catch (Exception ex)
        {
            _escritor = null;
            CaminhoArquivo = null;
            _logger.Warning($"Não foi possível criar o relatório de desempenho: {ex.Message}");
        }
    }

    // ---------------------------------------------------------------- Escrita imediata

    private void EscreverCabecalho()
    {
        if (_escritor is null) return;

        Titulo("RadarTorres — Relatório de Desempenho");
        _escritor.WriteLine($" Execução iniciada em {_inicio:dddd, dd 'de' MMMM 'de' yyyy 'às' HH:mm:ss}".ToString(Cultura));
        _escritor.WriteLine(new string('=', 78));
        _escritor.WriteLine();

        Secao("AMBIENTE");
        Item("Aplicativo", $"RadarTorres {Assembly.GetExecutingAssembly().GetName().Version}");
        Item("Plataforma", $"{RuntimeInformation.FrameworkDescription} / {RuntimeInformation.RuntimeIdentifier}");
        Item("Sistema operacional", RuntimeInformation.OSDescription);
        Item("Processo", Environment.Is64BitProcess ? "64 bits" : "32 bits");
        Item("Máquina", $"{Environment.MachineName} (usuário do Windows: {Environment.UserName})");
        _escritor.WriteLine();

        Secao("RECURSOS COMPUTACIONAIS");
        Item("Processadores lógicos", Environment.ProcessorCount.ToString(Cultura));
        Item("Arquitetura do processador", RuntimeInformation.ProcessArchitecture.ToString());
        EscreverMemoria();
        EscreverTela();
        EscreverDisco();
        _escritor.WriteLine();

        EscreverParametros();

        Secao("LINHA DO TEMPO DA SESSÃO");
    }

    private void EscreverMemoria()
    {
        if (_escritor is null) return;

        if (TentarLerMemoriaFisica(out ulong totalBytes, out ulong disponivelBytes))
        {
            Item("Memória física total", FormatarBytes(totalBytes));
            Item("Memória disponível", $"{FormatarBytes(disponivelBytes)} (no início da sessão)");
        }
        else
        {
            // Sem a API do Windows, o limite visto pelo coletor de lixo ainda dá uma ordem de
            // grandeza útil — melhor que omitir a linha e deixar a dúvida no relatório.
            Item("Memória disponível ao processo", FormatarBytes((ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes));
        }
    }

    private void EscreverTela()
    {
        if (_escritor is null) return;

        try
        {
            // Resolução entra no relatório porque o custo da detecção depende diretamente do
            // tamanho do quadro, e a janela do aplicativo se dimensiona pela tela.
            Item("Resolução da tela principal",
                $"{SystemParameters.PrimaryScreenWidth:0} x {SystemParameters.PrimaryScreenHeight:0}");
        }
        catch (Exception)
        {
            // Sem sessão gráfica (execução automatizada) não há tela — não é erro.
        }
    }

    private void EscreverDisco()
    {
        if (_escritor is null) return;

        try
        {
            var unidade = new DriveInfo(Path.GetPathRoot(AppDataPaths.RootFolder) ?? "C:\\");
            Item("Disco dos dados",
                $"{unidade.Name} — {FormatarBytes((ulong)unidade.AvailableFreeSpace)} livres de {FormatarBytes((ulong)unidade.TotalSize)}");
        }
        catch (Exception ex)
        {
            Item("Disco dos dados", $"(não foi possível consultar: {ex.Message})");
        }
    }

    private void EscreverParametros()
    {
        if (_escritor is null) return;

        VisionSettings v = AppConfig.Current.VisionSettings;

        Secao("PARÂMETROS DE VISÃO EM VIGOR");
        _escritor.WriteLine("  Valores realmente carregados (appsettings.json sobrepõe os padrões do código).");
        _escritor.WriteLine();
        Item("ConfiancaMinima", N(v.ConfiancaMinima));
        Item("IntervaloProcessamentoMs", v.IntervaloProcessamentoMs.ToString(Cultura));
        Item("LimiarSimilaridadeFoto", N(v.LimiarSimilaridadeFoto));
        Item("PesoHistogramaCor", N(v.PesoHistogramaCor));
        Item("LimiarCorCandidato", N(v.LimiarCorCandidato));
        Item("SaturacaoMinimaCor", N(v.SaturacaoMinimaCor));
        Item("BrilhoMinimoCor", N(v.BrilhoMinimoCor));
        Item("AreaMinimaRegiaoCor", N(v.AreaMinimaRegiaoCor));
        Item("LimiarPreenchimentoMinimo", N(v.LimiarPreenchimentoMinimo));
        Item("LimiarPreenchimentoQuadrado", N(v.LimiarPreenchimentoQuadrado));
        Item("LimiarAspectoRetangular", N(v.LimiarAspectoRetangular));
        Item("DebounceAlertaSegundos", v.DebounceAlertaSegundos.ToString(Cultura));
        Item("CameraFovHorizontalGraus", N(v.CameraFovHorizontalGraus));
        _escritor.WriteLine();
    }

    public void RegistrarEvento(string descricao)
    {
        lock (_trava)
        {
            if (_escritor is null || _finalizado) return;

            _eventos++;
            if (_eventos > MaxEventos) return;

            try
            {
                _escritor.WriteLine(_eventos == MaxEventos
                    ? $"  [{DateTime.Now:HH:mm:ss}] (limite de eventos atingido — demais ocorrências omitidas)"
                    : $"  [{DateTime.Now:HH:mm:ss}] {descricao}");
            }
            catch (Exception ex)
            {
                _logger.Warning($"Falha ao anotar evento no relatório de desempenho: {ex.Message}");
            }
        }
    }

    public void RegistrarErro(string contexto, string mensagem)
    {
        lock (_trava)
        {
            if (_escritor is null || _finalizado) return;

            _erros++;
            if (_erros > MaxErros) return;

            try
            {
                _escritor.WriteLine($"  [{DateTime.Now:HH:mm:ss}] *** ERRO ({contexto}): {mensagem}");
            }
            catch (Exception)
            {
                // Já estamos tratando um erro; não há a quem reportar a falha de reportá-lo.
            }
        }
    }

    public void RegistrarRodada(RegistroRodada r)
    {
        lock (_trava)
        {
            if (_escritor is null || _finalizado) return;

            _rodadas++;
            if (r.Tempos.DetectorExecutou) _rodadasComDetector++;
            if (r.Identificado) _rodadasIdentificadas++;

            _somaPreparoMs += r.Tempos.PreparoMs;
            _somaReferenciaMs += r.Tempos.ReferenciaMs;
            _somaSegmentacaoMs += r.Tempos.SegmentacaoCorMs;
            _somaDetectorMs += r.Tempos.DetectorMs;
            _somaComparacaoMs += r.Tempos.ComparacaoMs;

            _resolucaoQuadro = $"{r.LarguraQuadro} x {r.AlturaQuadro}";
            _perfilMaisRecente = r.PerfilAtivo;

            if (_duracoes.Count < MaxAmostras) _duracoes.Add(r.Tempos.TotalMs);

            if (r.TempoAteIdentificarMs is { } latencia && _latenciasAquisicao.Count < MaxAmostras)
            {
                _latenciasAquisicao.Add(latencia);
            }

            // Guarda a pontuação das rodadas que NÃO identificaram para o relatório poder dizer
            // se o limiar está perto demais do que a cena produz — o caso em que um ajuste
            // pequeno de calibração resolveria, e que de outra forma passaria despercebido.
            if (!r.Identificado && _pontuacoesRejeitadas.Count < MaxAmostras)
            {
                _pontuacoesRejeitadas.Add(r.MelhorPontuacao);
            }
        }
    }

    // ---------------------------------------------------------------- Encerramento

    /// <summary>
    /// Fecha o relatório escrevendo as estatísticas. Chamado pelo contêiner de DI no
    /// encerramento do aplicativo (ver <c>App.OnExit</c>, que faz <c>ServiceProvider.Dispose()</c>).
    /// </summary>
    public void Dispose()
    {
        lock (_trava)
        {
            if (_escritor is null || _finalizado) return;
            _finalizado = true;

            try
            {
                EscreverFechamento();
            }
            catch (Exception)
            {
                // Encerrando o processo: uma exceção aqui impediria o StreamWriter de fechar e
                // perderia inclusive o que já tinha sido gravado.
            }
            finally
            {
                _escritor.Dispose();
            }
        }
    }

    private void EscreverFechamento()
    {
        if (_escritor is null) return;

        if (_eventos == 0) _escritor.WriteLine("  (nenhum evento registrado)");
        _escritor.WriteLine();

        EscreverErros();
        EscreverEstatisticasDeteccao();
        EscreverConsumoProcesso();
        EscreverObservacoes();

        _escritor.WriteLine(new string('=', 78));
        _escritor.WriteLine($" Fim do relatório — {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        _escritor.WriteLine(new string('=', 78));
    }

    private void EscreverErros()
    {
        if (_escritor is null) return;

        Secao("ERROS");

        if (_erros == 0)
        {
            _escritor.WriteLine("  Nenhum erro registrado nesta execução.");
        }
        else
        {
            _escritor.WriteLine($"  {_erros} erro(s) registrado(s) — detalhados na linha do tempo acima, marcados com '*** ERRO'.");
            if (_erros > MaxErros) _escritor.WriteLine($"  (apenas os {MaxErros} primeiros foram detalhados)");
        }

        _escritor.WriteLine();
    }

    private void EscreverEstatisticasDeteccao()
    {
        if (_escritor is null) return;

        TimeSpan sessao = DateTime.Now - _inicio;

        Secao("DETECÇÃO POR CÂMERA");
        Item("Duração da sessão", $"{sessao:hh\\:mm\\:ss}");

        if (_rodadas == 0)
        {
            _escritor.WriteLine("  Nenhuma rodada de detecção executada — a câmera não foi usada nesta execução.");
            _escritor.WriteLine();
            return;
        }

        Item("Resolução do quadro", _resolucaoQuadro);
        Item("Perfil de alvo", _perfilMaisRecente);
        Item("Rodadas executadas", _rodadas.ToString(Cultura));
        Item("Rodadas com alvo identificado", $"{_rodadasIdentificadas} ({Fracao(_rodadasIdentificadas, _rodadas)})");
        Item("Rodadas que exigiram o modelo ONNX", $"{_rodadasComDetector} ({Fracao(_rodadasComDetector, _rodadas)})");
        _escritor.WriteLine();

        _escritor.WriteLine("  Tempo por rodada (ms):");
        Item("    mínimo", N(_duracoes.Min()));
        Item("    mediana", N(Percentil(_duracoes, 50)));
        Item("    p95", N(Percentil(_duracoes, 95)));
        Item("    máximo", N(_duracoes.Max()));
        Item("    média", N(_duracoes.Average()));
        Item("    taxa efetiva", $"{N(1000.0 / Math.Max(0.001, _duracoes.Average()))} rodadas/s");
        _escritor.WriteLine();

        _escritor.WriteLine("  Custo médio por etapa (ms por rodada):");
        Item("    preparo do quadro", N(_somaPreparoMs / _rodadas));
        Item("    foto de referência", N(_somaReferenciaMs / _rodadas));
        Item("    segmentação por cor", N(_somaSegmentacaoMs / _rodadas));
        Item("    comparação de candidatos", N(_somaComparacaoMs / _rodadas));
        Item("    modelo ONNX (sobre todas)", N(_somaDetectorMs / _rodadas));
        Item("    modelo ONNX (quando rodou)", _rodadasComDetector == 0 ? "não rodou" : N(_somaDetectorMs / _rodadasComDetector));
        _escritor.WriteLine();

        if (_latenciasAquisicao.Count > 0)
        {
            _escritor.WriteLine($"  Latência de aquisição do alvo — intervalo entre o alvo deixar de ser");
            _escritor.WriteLine($"  reconhecido e voltar a ser ({_latenciasAquisicao.Count} aquisições, ms):");
            Item("    mínimo", N(_latenciasAquisicao.Min()));
            Item("    mediana", N(Percentil(_latenciasAquisicao, 50)));
            Item("    p95", N(Percentil(_latenciasAquisicao, 95)));
            Item("    máximo", N(_latenciasAquisicao.Max()));
        }
        else
        {
            _escritor.WriteLine("  Latência de aquisição: nenhuma aquisição ocorreu (o alvo nunca foi reconhecido).");
        }

        _escritor.WriteLine();
    }

    private void EscreverConsumoProcesso()
    {
        if (_escritor is null) return;

        Secao("CONSUMO DE RECURSOS DO PROCESSO");

        try
        {
            using Process processo = Process.GetCurrentProcess();
            TimeSpan sessao = DateTime.Now - _inicio;

            Item("Memória de trabalho (atual)", FormatarBytes((ulong)processo.WorkingSet64));
            Item("Memória de trabalho (pico)", FormatarBytes((ulong)processo.PeakWorkingSet64));
            Item("Tempo total de CPU", $"{processo.TotalProcessorTime.TotalSeconds:0.0} s");

            if (sessao.TotalSeconds > 1)
            {
                // Normalizado pelo número de núcleos: 100% = a máquina inteira ocupada.
                double usoMedio = processo.TotalProcessorTime.TotalSeconds
                    / (sessao.TotalSeconds * Environment.ProcessorCount);
                Item("Uso médio de CPU", $"{usoMedio:P1} do total disponível na máquina");
            }

            Item("Threads", processo.Threads.Count.ToString(Cultura));
            Item("Coletas de lixo (G0/G1/G2)", $"{GC.CollectionCount(0)} / {GC.CollectionCount(1)} / {GC.CollectionCount(2)}");
        }
        catch (Exception ex)
        {
            _escritor.WriteLine($"  (não foi possível consultar: {ex.Message})");
        }

        _escritor.WriteLine();
    }

    /// <summary>
    /// Conclusões automáticas a partir dos números coletados. Existe para o relatório ser útil
    /// a quem não conhece o pipeline por dentro: em vez de só despejar estatísticas, aponta o
    /// que elas sugerem fazer.
    /// </summary>
    private void EscreverObservacoes()
    {
        if (_escritor is null) return;

        Secao("OBSERVAÇÕES AUTOMÁTICAS");

        var notas = new List<string>();

        if (_rodadas == 0)
        {
            notas.Add("A câmera não foi usada: não há o que analisar sobre a detecção nesta execução.");
        }
        else
        {
            double proporcaoDetector = (double)_rodadasComDetector / _rodadas;
            if (proporcaoDetector > 0.5)
            {
                notas.Add(
                    $"O modelo ONNX precisou rodar em {Fracao(_rodadasComDetector, _rodadas)} das rodadas. " +
                    "Isso significa que a segmentação pela cor do perfil não está encontrando o alvo e o " +
                    "caminho caro está sendo acionado. Confira se a cor cadastrada no perfil corresponde " +
                    "mesmo ao objeto e se a iluminação não está lavando a cor.");
            }

            if (_rodadasIdentificadas == 0)
            {
                notas.Add("O alvo nunca foi reconhecido. Verifique se há perfil ativo e se o objeto apareceu no quadro.");
            }

            if (_pontuacoesRejeitadas.Count > 0)
            {
                double limiar = AppConfig.Current.VisionSettings.LimiarSimilaridadeFoto;
                double p95Rejeitada = Percentil(_pontuacoesRejeitadas, 95);

                if (p95Rejeitada > limiar * 0.85 && _rodadasIdentificadas < _rodadas / 2)
                {
                    notas.Add(
                        $"As rodadas que NÃO identificaram chegaram a {N(p95Rejeitada)} de pontuação (p95), " +
                        $"perto do limiar de {N(limiar)}. O alvo pode estar sendo reprovado por pouco — " +
                        "vale testar LimiarSimilaridadeFoto um pouco menor.");
                }

                if (p95Rejeitada < limiar * 0.5 && _rodadasIdentificadas > 0)
                {
                    notas.Add(
                        $"As rodadas que não identificaram ficaram bem abaixo do limiar (p95 = {N(p95Rejeitada)} " +
                        $"contra limiar {N(limiar)}), ou seja, há margem folgada separando alvo de não-alvo. " +
                        "Dá para apertar o limiar sem perder detecção, reduzindo risco de falso positivo.");
                }
            }

            if (_duracoes.Count > 0)
            {
                double mediana = Percentil(_duracoes, 50);
                double maiorEtapa = Math.Max(
                    Math.Max(_somaPreparoMs, _somaSegmentacaoMs),
                    Math.Max(_somaDetectorMs, _somaComparacaoMs)) / _rodadas;

                string nomeMaior =
                    maiorEtapa * _rodadas == _somaPreparoMs ? "preparo do quadro"
                    : maiorEtapa * _rodadas == _somaSegmentacaoMs ? "segmentação por cor"
                    : maiorEtapa * _rodadas == _somaDetectorMs ? "modelo ONNX"
                    : "comparação de candidatos";

                notas.Add($"Etapa mais cara: {nomeMaior} ({N(maiorEtapa)} ms de {N(mediana)} ms por rodada). " +
                          "É por aí que uma próxima otimização dá mais retorno.");

                int intervalo = AppConfig.Current.VisionSettings.IntervaloProcessamentoMs;
                if (mediana > intervalo)
                {
                    notas.Add(
                        $"A rodada mediana ({N(mediana)} ms) é mais lenta que IntervaloProcessamentoMs ({intervalo} ms): " +
                        "o ritmo real é ditado pelo custo da rodada, e baixar mais o intervalo não terá efeito.");
                }
            }
        }

        if (_erros > 0)
        {
            notas.Add($"Houve {_erros} erro(s) nesta execução — veja a linha do tempo antes de confiar nas estatísticas acima.");
        }

        if (notas.Count == 0) notas.Add("Nada fora do esperado nesta execução.");

        foreach (string nota in notas)
        {
            _escritor.WriteLine($"  • {QuebrarLinha(nota, 72, "    ")}");
        }

        _escritor.WriteLine();
    }

    // ---------------------------------------------------------------- Utilidades de formatação

    private void Titulo(string texto)
    {
        if (_escritor is null) return;
        _escritor.WriteLine(new string('=', 78));
        _escritor.WriteLine($" {texto}");
    }

    private void Secao(string nome)
    {
        if (_escritor is null) return;
        _escritor.WriteLine($"[{nome}]");
    }

    /// <summary>Linha "rótulo ....... valor", com os pontos alinhando a coluna de valores.</summary>
    private void Item(string rotulo, string valor)
    {
        if (_escritor is null) return;

        const int coluna = 34;
        string prefixo = $"  {rotulo} ";
        string pontos = prefixo.Length >= coluna ? " " : new string('.', coluna - prefixo.Length);
        _escritor.WriteLine($"{prefixo}{pontos} {valor}");
    }

    private static string N(double valor) => valor.ToString("0.###", Cultura);

    private static string Fracao(int parte, int total) =>
        total == 0 ? "0%" : ((double)parte / total).ToString("P1", Cultura);

    private static string FormatarBytes(ulong bytes)
    {
        string[] unidades = ["B", "KB", "MB", "GB", "TB"];
        double valor = bytes;
        int indice = 0;

        while (valor >= 1024 && indice < unidades.Length - 1)
        {
            valor /= 1024;
            indice++;
        }

        return $"{valor.ToString("0.#", Cultura)} {unidades[indice]}";
    }

    /// <summary>Quebra um texto longo em várias linhas indentadas, para o relatório não exigir rolagem horizontal.</summary>
    private static string QuebrarLinha(string texto, int largura, string indentacao)
    {
        var resultado = new StringBuilder();
        int colunaAtual = 0;

        foreach (string palavra in texto.Split(' '))
        {
            if (colunaAtual > 0 && colunaAtual + palavra.Length + 1 > largura)
            {
                resultado.Append(Environment.NewLine).Append(indentacao);
                colunaAtual = indentacao.Length;
            }
            else if (colunaAtual > 0)
            {
                resultado.Append(' ');
                colunaAtual++;
            }

            resultado.Append(palavra);
            colunaAtual += palavra.Length;
        }

        return resultado.ToString();
    }

    /// <summary>Percentil pelo vizinho mais próximo — suficiente para diagnóstico, sem dependência externa.</summary>
    private static double Percentil(List<double> valores, double percentil)
    {
        if (valores.Count == 0) return 0;

        List<double> ordenados = valores.OrderBy(v => v).ToList();
        int indice = (int)Math.Round((percentil / 100.0) * (ordenados.Count - 1), MidpointRounding.AwayFromZero);
        return ordenados[Math.Clamp(indice, 0, ordenados.Count - 1)];
    }

    // ---------------------------------------------------------------- Memória física (Win32)

    /// <summary>
    /// Memória física total e disponível. Via Win32 porque o .NET não expõe a RAM física da
    /// máquina de forma portátil — <see cref="GC"/> só conhece o limite do próprio processo, que
    /// é outra informação.
    /// </summary>
    private static bool TentarLerMemoriaFisica(out ulong totalBytes, out ulong disponivelBytes)
    {
        totalBytes = 0;
        disponivelBytes = 0;

        try
        {
            var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (!GlobalMemoryStatusEx(ref status)) return false;

            totalBytes = status.ullTotalPhys;
            disponivelBytes = status.ullAvailPhys;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }
}
