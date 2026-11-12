using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RadarTorres.App.Configuration;
using RadarTorres.App.NeuralEvolution;
using SharpNeat.IO;
using SharpNeat.IO.Models;
using SharpNeat.Neat.Genome;
using SharpNeat.Neat.Genome.IO;
using SharpNeat.NeuralNets.IO;

namespace RadarTorres.Experimentos;

/// <summary>
/// Executor de experimentos de neuroevolução fora da UI (a tela de treinamento não é adequada
/// para baterias longas). Carrega o MESMO appsettings.json de src/RadarTorres.App para que o
/// layout de torres seja idêntico ao do aplicativo — sem isso os resultados não seriam
/// comparáveis com os registrados em experimentos_hiperparametros.
/// Nada aqui altera a função de fitness: ambiente, recompensas e critérios de avaliação são os
/// de <see cref="ParametrosTreinamento"/> e <see cref="AmbienteTreinamento"/>.
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CarregarConfiguracao();

        string comando = args.Length > 0 ? args[0] : "teto";
        switch (comando)
        {
            case "teto": Teto(args); return 0;
            case "ga": Ga(args); return 0;
            case "ablacao": Ablacao(args); return 0;
            case "hiperparametros": Hiperparametros(); return 0;
            case "sobrevivencia": Sobrevivencia(args); return 0;
            case "v2": V2(args); return 0;
            default:
                Console.WriteLine("Comandos: teto | ga | ablacao | hiperparametros | sobrevivencia | v2");
                return 1;
        }
    }

    private static void CarregarConfiguracao()
    {
        string? raiz = AppContext.BaseDirectory;
        string? caminho = null;
        while (raiz is not null)
        {
            string candidato = Path.Combine(raiz, "src", "RadarTorres.App", "appsettings.json");
            if (File.Exists(candidato)) { caminho = candidato; break; }
            raiz = Path.GetDirectoryName(raiz);
        }

        if (caminho is null)
        {
            Console.WriteLine("ERRO: appsettings.json de src/RadarTorres.App nao encontrado — abortando (as torres seriam diferentes das do app).");
            Environment.Exit(2);
            return;
        }

        AppSettings settings = new ConfigurationBuilder()
            .AddJsonFile(caminho, optional: false)
            .Build()
            .Get<AppSettings>() ?? new AppSettings();

        AppConfig.Current = settings;
        Console.WriteLine($"appsettings: {caminho}");
        Console.WriteLine($"torres: {string.Join(" ", settings.Towers.Select(t => $"#{t.Id}({t.X};{t.Y})"))}");
    }

    private static double Arg(string[] a, string nome, double padrao)
    {
        string? v = a.FirstOrDefault(x => x.StartsWith("--" + nome + "=", StringComparison.Ordinal));
        return v is null ? padrao : double.Parse(v.Split('=')[1], CultureInfo.InvariantCulture);
    }

    private static string ArgS(string[] a, string nome, string padrao)
    {
        string? v = a.FirstOrDefault(x => x.StartsWith("--" + nome + "=", StringComparison.Ordinal));
        return v is null ? padrao : v.Split('=')[1];
    }

    // ---------------------------------------------------------------- teto

    /// <summary>
    /// Mede o TETO de fitness do ambiente: quantos alvos nascem por episódio e quanto valeria
    /// abater todos. Nada é estimado no papel — os alvos são contados rodando o próprio
    /// <see cref="AmbienteTreinamento"/>.
    /// </summary>
    private static void Teto(string[] args)
    {
        int episodios = (int)Arg(args, "episodios", 200);
        var p = new ParametrosTreinamento
        {
            DuracaoEpisodioSegundos = Arg(args, "dur", 45),
            IntervaloSpawnMinSegundos = Arg(args, "spawnmin", 1.5),
            IntervaloSpawnMaxSegundos = Arg(args, "spawnmax", 4.0)
        };
        var ambiente = new AmbienteTreinamento(p);

        // Rede nula => devolve tanh(0)=0 em tudo => confiança de disparo nunca > 0, ou seja,
        // controlador passivo: nenhum abate. Serve para contar nascimentos no pior caso (teto de
        // simultâneos sempre ocupado).
        RedeNeat nulo = RedeNula();

        var nascidosPassivo = new List<int>();
        var nascidosHeuristico = new List<int>();
        var abatidosHeuristico = new List<int>();
        var fitnessHeuristico = new List<double>();

        for (int s = 0; s < episodios; s++)
        {
            nascidosPassivo.Add(ambiente.ExecutarEpisodioRapido(nulo, s).AlvosNascidos);
            EpisodioResultado h = ambiente.ExecutarEpisodioRapido(nulo, s, TipoCenarioTreinamento.Aleatorio, ControladorHibrido.ModoDecisao.HeuristicaPura);
            nascidosHeuristico.Add(h.AlvosNascidos);
            abatidosHeuristico.Add(h.AlvosAbatidos);
            fitnessHeuristico.Add(h.FitnessTotal);
        }

        Console.WriteLine($"--- TETO (duracao={p.DuracaoEpisodioSegundos}s, spawn={p.IntervaloSpawnMinSegundos}-{p.IntervaloSpawnMaxSegundos}s, {episodios} episodios, seeds 0..{episodios - 1}) ---");
        Console.WriteLine($"nascidos (passivo)         : media={nascidosPassivo.Average():0.00} min={nascidosPassivo.Min()} max={nascidosPassivo.Max()}");
        Console.WriteLine($"nascidos (heuristica pura) : media={nascidosHeuristico.Average():0.00} min={nascidosHeuristico.Min()} max={nascidosHeuristico.Max()}");
        Console.WriteLine($"TETO ABSOLUTO (todos abatidos na borda, 10/alvo): media={nascidosHeuristico.Average() * 10:0.0} max={nascidosHeuristico.Max() * 10:0.0}");
        Console.WriteLine($"heuristica pura: abatidos media={abatidosHeuristico.Average():0.00} fitness media={fitnessHeuristico.Average():0.00} fitness max={fitnessHeuristico.Max():0.00}");
    }

    // ------------------------------------------------------------------ ga

    private sealed record Execucao(
        string Rotulo, int Populacao, int Ocultos, double Mutacao, int Geracoes, int Seed,
        int MaxAlvos, string Modo, bool CenariosFixos,
        double MelhorFitnessTreino, int GeracaoDoPico, double FitnessFinalGeracao, double FitnessMedioGeracaoFinal,
        double SegundosTotais, double ValFitnessMedio, double ValFitnessMax, double ValAlvosNascidos,
        double ValAbatidos, double ValEscapados, double ValFitnessPorAlvo, double ValTaxaAbate);

    private static void Ga(string[] args)
    {
        var modo = Enum.Parse<ControladorHibrido.ModoDecisao>(ArgS(args, "modo", "Hibrido"));
        Gravar(RodarGa(
            ArgS(args, "rotulo", "sem-rotulo"),
            (int)Arg(args, "pop", 60),
            (int)Arg(args, "ocultos", 8),
            Arg(args, "mut", 0.08),
            (int)Arg(args, "ger", 300),
            (int)Arg(args, "seed", 1),
            (int)Arg(args, "maxalvos", 5),
            modo,
            ArgS(args, "cenarios", "sim") == "sim",
            Arg(args, "limite", 0),
            Arg(args, "dur", 45),
            Arg(args, "spawnmin", 1.5),
            Arg(args, "spawnmax", 4.0),
            ArgS(args, "semlimite", "nao") == "sim"));
    }

    private static Execucao RodarGa(
        string rotulo, int populacao, int ocultos, double mutacao, int geracoes, int seed,
        int maxAlvos, ControladorHibrido.ModoDecisao modo, bool cenariosFixos, double limiteSegundos,
        double duracao = 45, double spawnMin = 1.5, double spawnMax = 4.0, bool semLimiteDeDuracao = false)
    {
        // "ocultos" deixou de ser a largura da camada oculta (o NEAT evolui a topologia) e passou
        // a ser o TETO DE COMPLEXIDADE em conexões; "mutacao" passou a ser a probabilidade de
        // mutação estrutural. Os nomes dos argumentos de linha de comando foram mantidos para não
        // invalidar os scripts e as execuções já registradas em Resultados/.
        int tetoComplexidade = ocultos;
        var parametros = new ParametrosTreinamento
        {
            TamanhoPopulacao = populacao,
            ProbabilidadeAdicionarNo = mutacao,
            TetoComplexidade = tetoComplexidade,
            DuracaoEpisodioSegundos = duracao,
            IntervaloSpawnMinSegundos = spawnMin,
            IntervaloSpawnMaxSegundos = spawnMax
        };
        var ambiente = new AmbienteTreinamento(parametros);
        var ag = new TreinadorNeat(
            ambiente,
            tamanhoPopulacao: populacao,
            quantidadeEspecies: parametros.QuantidadeEspecies,
            tetoComplexidade: tetoComplexidade,
            probabilidadeAdicionarNo: mutacao);

        IReadOnlyList<TipoCenarioTreinamento>? fixos = cenariosFixos ? CatalogoCenarios.TodosComScript : null;

        var cronometro = Stopwatch.StartNew();
        double melhor = double.NegativeInfinity;
        int geracaoPico = 0;
        NeatGenome<double>? melhorGenoma = null;
        ResultadoGeracao? ultimo = null;
        int geracoesRodadas = 0;

        for (int g = 0; g < geracoes; g++)
        {
            int[] seeds = Enumerable.Range(0, Math.Max(1, parametros.EpisodiosPorAvaliacao))
                .Select(i => ag.GeracaoAtual * 1000 + i)
                .ToArray();

            ultimo = ag.EvoluirGeracao(seeds, fixos, modo);
            geracoesRodadas = ultimo.Geracao;

            if (ultimo.MelhorFitness > melhor)
            {
                melhor = ultimo.MelhorFitness;
                geracaoPico = ultimo.Geracao;
                melhorGenoma = ultimo.MelhorGenoma;
            }

            if (ultimo.Geracao % 200 == 0)
            {
                Console.WriteLine($"[{rotulo}] ger {ultimo.Geracao}: melhor={ultimo.MelhorFitness:0.00} pico={melhor:0.00} medio={ultimo.FitnessMedio:0.00} t={cronometro.Elapsed.TotalSeconds:0}s");
            }

            if (limiteSegundos > 0 && cronometro.Elapsed.TotalSeconds > limiteSegundos)
            {
                Console.WriteLine($"[{rotulo}] INTERROMPIDO por limite de tempo na geracao {ultimo.Geracao}.");
                break;
            }
        }

        cronometro.Stop();

        // Validação fora das seeds de treino (90000..90049, nunca vistas pelo AG). É o número
        // honesto: o "melhor fitness" do AG é otimista, por ser o máximo sobre a população nas
        // seeds daquela geração específica.
        var ambienteVal = new AmbienteTreinamento(parametros);
        RedeNeat melhorRede = melhorGenoma is null ? RedeNula() : ag.Decodificar(melhorGenoma);
        var fit = new List<double>();
        var nasc = new List<int>();
        var ab = new List<int>();
        var esc = new List<int>();
        for (int s = 90000; s < 90050; s++)
        {
            EpisodioResultado e = ambienteVal.ExecutarEpisodioRapido(melhorRede, s, TipoCenarioTreinamento.Aleatorio, modo);
            fit.Add(e.FitnessTotal); nasc.Add(e.AlvosNascidos); ab.Add(e.AlvosAbatidos); esc.Add(e.AlvosEscapados);
        }

        var resultado = new Execucao(
            rotulo, populacao, ocultos, mutacao, geracoesRodadas, seed, maxAlvos, modo.ToString(), cenariosFixos,
            melhor, geracaoPico, ultimo?.MelhorFitness ?? 0, ultimo?.FitnessMedio ?? 0,
            cronometro.Elapsed.TotalSeconds,
            fit.Average(), fit.Max(), nasc.Average(), ab.Average(), esc.Average(),
            fit.Average() / Math.Max(1e-9, nasc.Average()),
            ab.Average() / Math.Max(1e-9, nasc.Average()));

        Console.WriteLine($"[{rotulo}] FIM ger={geracoesRodadas} pico_treino={melhor:0.00}(g{geracaoPico}) | VAL fitness={resultado.ValFitnessMedio:0.00} (max {resultado.ValFitnessMax:0.00}) nascidos={resultado.ValAlvosNascidos:0.00} abatidos={resultado.ValAbatidos:0.00} escapados={resultado.ValEscapados:0.00} fit/alvo={resultado.ValFitnessPorAlvo:0.00} taxaAbate={resultado.ValTaxaAbate:0.000} | {cronometro.Elapsed.TotalSeconds:0}s");

        if (melhorGenoma is not null) SalvarRede(rotulo, melhorGenoma);
        return resultado;
    }

    // ------------------------------------------------------------- ablacao

    /// <summary>Compara Hibrido x RedePura x HeuristicaPura sob a MESMA seed de AG e os mesmos parâmetros.</summary>
    private static void Ablacao(string[] args)
    {
        int geracoes = (int)Arg(args, "ger", 200);
        int seed = (int)Arg(args, "seed", 1);
        int maxAlvos = (int)Arg(args, "maxalvos", 5);
        bool cenariosFixos = ArgS(args, "cenarios", "nao") == "sim";
        double limite = Arg(args, "limite", 0);

        foreach (ControladorHibrido.ModoDecisao modo in new[] { ControladorHibrido.ModoDecisao.Hibrido, ControladorHibrido.ModoDecisao.RedePura })
        {
            Gravar(RodarGa($"ablacao-{modo}", 60, 8, 0.08, geracoes, seed, maxAlvos, modo, cenariosFixos, limite));
        }

        // Heurística pura não tem o que evoluir (ignora a rede): basta avaliá-la nas mesmas
        // seeds de validação, com a rede nula.
        var parametros = new ParametrosTreinamento();
        var amb = new AmbienteTreinamento(parametros);
        RedeNeat nulo = RedeNula();
        var fit = new List<double>(); var nasc = new List<int>(); var ab = new List<int>(); var esc = new List<int>();
        for (int s = 90000; s < 90050; s++)
        {
            EpisodioResultado e = amb.ExecutarEpisodioRapido(nulo, s, TipoCenarioTreinamento.Aleatorio, ControladorHibrido.ModoDecisao.HeuristicaPura);
            fit.Add(e.FitnessTotal); nasc.Add(e.AlvosNascidos); ab.Add(e.AlvosAbatidos); esc.Add(e.AlvosEscapados);
        }
        var heur = new Execucao("ablacao-HeuristicaPura", 0, 0, 0, 0, seed, maxAlvos, "HeuristicaPura", cenariosFixos,
            0, 0, 0, 0, 0, fit.Average(), fit.Max(), nasc.Average(), ab.Average(), esc.Average(),
            fit.Average() / Math.Max(1e-9, nasc.Average()), ab.Average() / Math.Max(1e-9, nasc.Average()));
        Console.WriteLine($"[ablacao-HeuristicaPura] VAL fitness={heur.ValFitnessMedio:0.00} nascidos={heur.ValAlvosNascidos:0.00} abatidos={heur.ValAbatidos:0.00} taxaAbate={heur.ValTaxaAbate:0.000}");
        Gravar(heur);
    }

    // --------------------------------------------------- sobrevivencia (teto: rede ou ambiente?)

    /// <summary>
    /// Carrega uma rede já treinada (formato .net salvo por <see cref="SalvarRede"/> OU pela
    /// tela "Treinamento do Controlador") e avalia quanto ele realmente aguenta — o episódio não
    /// tem mais corte por duração, só termina quando um alvo de fato rompe uma torre ou ao
    /// atingir <see cref="ParametrosTreinamento.DuracaoMaximaSegurancaSegundos"/>.
    /// </summary>
    private static void Sobrevivencia(string[] args)
    {
        string caminho = ArgS(args, "genoma", "");
        if (caminho.Length == 0 || !File.Exists(caminho))
        {
            Console.WriteLine("Uso: sobrevivencia --genoma=<caminho.net> [--episodios=20]");
            return;
        }

        RedeNeat redeCarregada = CarregarRede(caminho);
        int episodios = (int)Arg(args, "episodios", 20);
        var modo = Enum.Parse<ControladorHibrido.ModoDecisao>(ArgS(args, "modo", "Hibrido"));

        var parametros = new ParametrosTreinamento();
        var ambiente = new AmbienteTreinamento(parametros);

        var fit = new List<double>();
        var nasc = new List<int>();
        var ab = new List<int>();
        var esc = new List<int>();
        var duracao = new List<double>();
        var oportunidades = new List<int>();
        var recusados = new List<int>();

        for (int s = 0; s < episodios; s++)
        {
            EstadoEpisodio estado = ambiente.NovoEpisodio(seed: s);
            var controlador = new ControladorHibrido(redeCarregada, parametros.RaioMundoMetros, parametros.VelocidadeMaximaAlvoMetrosPorSegundo, modo, parametros.TempoDeteccaoRelevanteSegundos);

            const double dt = 0.2;
            while (!estado.Finalizado) ambiente.Avancar(estado, controlador, dt);

            fit.Add(estado.FitnessAcumulado);
            nasc.Add(estado.AlvosNascidos);
            ab.Add(estado.AlvosAbatidos);
            esc.Add(estado.AlvosEscapados);
            duracao.Add(estado.TempoDecorridoSegundos);
            oportunidades.Add(estado.OportunidadesDeDisparo);
            recusados.Add(estado.DisparosRecusadosPelaRede);

            Console.WriteLine($"  seed {s}: fitness={estado.FitnessAcumulado:0.00} nascidos={estado.AlvosNascidos} abatidos={estado.AlvosAbatidos} escapados={estado.AlvosEscapados} duracao={estado.TempoDecorridoSegundos:0.0}s oportunidades={estado.OportunidadesDeDisparo} recusados={estado.DisparosRecusadosPelaRede}");
        }

        double media = fit.Average();
        double desvioPadrao = Math.Sqrt(fit.Sum(f => Math.Pow(f - media, 2)) / fit.Count);
        int totalOportunidades = oportunidades.Sum();
        int totalRecusados = recusados.Sum();

        Console.WriteLine($"--- SOBREVIVENCIA ({episodios} episodios, modo={modo}, genoma de {caminho}) ---");
        Console.WriteLine($"fitness   : media={fit.Average():0.00} desvio={desvioPadrao:0.00} min={fit.Min():0.00} max={fit.Max():0.00}");
        Console.WriteLine($"nascidos  : media={nasc.Average():0.00} min={nasc.Min()} max={nasc.Max()}");
        Console.WriteLine($"abatidos  : media={ab.Average():0.00}");
        Console.WriteLine($"disparo   : oportunidades={totalOportunidades} recusados={totalRecusados} taxaRecusa={(totalOportunidades > 0 ? (double)totalRecusados / totalOportunidades : 0):P1}");
        Console.WriteLine($"escapados : media={esc.Average():0.00}");
        Console.WriteLine($"duracao   : media={duracao.Average():0.0}s min={duracao.Min():0.0}s max={duracao.Max():0.0}s (ate invadir uma torre ou atingir a salvaguarda de {parametros.DuracaoMaximaSegurancaSegundos:0}s)");
    }

    /// <summary>
    /// Carrega uma rede do formato <c>.net</c> do SharpNEAT — o mesmo gravado por
    /// <see cref="SalvarRede"/> e pela tela "Treinamento do Controlador". O formato JSON antigo
    /// (arquitetura + vetor de pesos) não é mais aceito: descrevia uma rede de topologia fixa
    /// que não existe mais.
    /// </summary>
    private static RedeNeat CarregarRede(string caminho)
    {
        NetFileModel modelo = NetFile.Load(caminho);
        return new RedeNeat(NeuralNetConverter.ToNeuralNet(
            modelo,
            enableHardwareAcceleratedNeuralNets: false,
            enableHardwareAcceleratedActivationFunctions: false));
    }

    /// <summary>Rede inerte (todas as saídas em 0), usada como controlador passivo de referência.</summary>
    private static RedeNeat RedeNula()
    {
        var conexoes = new List<ConnectionLine>
        {
            new(0, RedeNeat.QuantidadeEntradasComBias, 0.0),
            new(0, RedeNeat.QuantidadeEntradasComBias + 1, 0.0),
        };
        var ativacoes = new List<ActivationFnLine> { new(0, TreinadorNeat.FuncaoAtivacao.ToString()) };

        NetFileModel modelo = NetFileModel.CreateAcyclic(
            RedeNeat.QuantidadeEntradasComBias, ControladorHibrido.QuantidadeSaidas, conexoes, ativacoes);

        return new RedeNeat(NeuralNetConverter.ToAcyclicNeuralNet(
            modelo,
            enableHardwareAcceleratedNeuralNets: false,
            enableHardwareAcceleratedActivationFunctions: false));
    }

    // --------------------------------------------------- hiperparametros (grid do artigo)

    private sealed record ConfiguracaoHiperparametro(int Populacao, int Ocultos, double Mutacao);

    private sealed record ResultadoHiperparametro(
        int Populacao, int Ocultos, double Mutacao, int Geracoes,
        double FitnessFinal, int GeracaoDoPico,
        double? FitnessG25, double? FitnessG50, double? FitnessG100, double? FitnessG200,
        double TempoSegundos);

    /// <summary>
    /// Reproduz, fora da UI, a MESMA bateria descrita no artigo (Seção "Análise experimental de
    /// hiperparâmetros"): três séries (camada oculta, população, taxa de mutação) de 300 gerações
    /// cada, com <see cref="CatalogoCenarios.TodosComScript"/>, usando exatamente a mesma lógica
    /// de <c>ExecutorExperimentosHiperparametros.TreinarConfiguracao</c> (seeds fixas por geração,
    /// elitismo 10% da população). Não depende de SQLite/WPF (esse executor roda fora da UI) —
    /// exporta direto para JSON.
    /// </summary>
    private static void Hiperparametros()
    {
        const int geracoesPorExperimento = 300;
        int[] geracoesDaCurva = [25, 50, 100, 200];

        var planejadas = (
            from pop in new[] { 30, 60, 120 }
            from oc in new[] { 4, 8, 16 }
            select new ConfiguracaoHiperparametro(pop, oc, 0.08))
            .Concat(new[] { 0.04, 0.16 }.Select(m => new ConfiguracaoHiperparametro(60, 8, m)))
            .ToList();

        Console.WriteLine($"Configurações planejadas: {planejadas.Count} ({ControladorHibrido.QuantidadeEntradas} entradas, {ControladorHibrido.QuantidadeSaidas} saídas; topologia evoluída pelo NEAT)");

        var resultados = new List<ResultadoHiperparametro>();
        int indice = 0;

        foreach (ConfiguracaoHiperparametro cfg in planejadas)
        {
            indice++;
            // cfg.Ocultos é o teto de complexidade e cfg.Mutacao a probabilidade de mutação
            // estrutural — ver a observação equivalente em Ga().
            var parametros = new ParametrosTreinamento
            {
                TamanhoPopulacao = cfg.Populacao,
                ProbabilidadeAdicionarNo = cfg.Mutacao,
                TetoComplexidade = cfg.Ocultos
            };
            var ambiente = new AmbienteTreinamento(parametros);
            var ag = new TreinadorNeat(
                ambiente,
                tamanhoPopulacao: cfg.Populacao,
                quantidadeEspecies: parametros.QuantidadeEspecies,
                tetoComplexidade: cfg.Ocultos,
                probabilidadeAdicionarNo: cfg.Mutacao);

            var cronometro = Stopwatch.StartNew();
            double melhorAteAgora = double.NegativeInfinity;
            int geracaoDoPico = 0;
            var curva = new Dictionary<int, double>();
            ResultadoGeracao? ultimo = null;

            for (int g = 0; g < geracoesPorExperimento; g++)
            {
                int[] seeds = Enumerable.Range(0, Math.Max(1, parametros.EpisodiosPorAvaliacao))
                    .Select(i => ag.GeracaoAtual * 1000 + i)
                    .ToArray();

                ultimo = ag.EvoluirGeracao(seeds, CatalogoCenarios.TodosComScript);

                if (ultimo.MelhorFitness > melhorAteAgora)
                {
                    melhorAteAgora = ultimo.MelhorFitness;
                    geracaoDoPico = ultimo.Geracao;
                }

                if (geracoesDaCurva.Contains(ultimo.Geracao)) curva[ultimo.Geracao] = melhorAteAgora;

                if (ultimo.Geracao % 50 == 0)
                {
                    Console.WriteLine($"  [{indice}/{planejadas.Count} pop={cfg.Populacao} ocultos={cfg.Ocultos} mut={cfg.Mutacao:0.##}] ger {ultimo.Geracao}/{geracoesPorExperimento} melhor={melhorAteAgora:0.00} (pico g{geracaoDoPico}) t={cronometro.Elapsed.TotalSeconds:0}s");
                }
            }

            cronometro.Stop();

            var resultado = new ResultadoHiperparametro(
                cfg.Populacao, cfg.Ocultos, cfg.Mutacao, geracoesPorExperimento,
                melhorAteAgora, geracaoDoPico,
                curva.TryGetValue(25, out double g25) ? g25 : null,
                curva.TryGetValue(50, out double g50) ? g50 : null,
                curva.TryGetValue(100, out double g100) ? g100 : null,
                curva.TryGetValue(200, out double g200) ? g200 : null,
                cronometro.Elapsed.TotalSeconds);

            resultados.Add(resultado);
            Console.WriteLine($"[{indice}/{planejadas.Count}] FIM pop={cfg.Populacao} ocultos={cfg.Ocultos} mut={cfg.Mutacao:0.##}: fitnessFinal={melhorAteAgora:0.00} picoG={geracaoDoPico} tempo={cronometro.Elapsed.TotalSeconds:0.0}s");
        }

        string jsonPath = Path.Combine(PastaResultados(), "experimentos_hiperparametros_7entradas.json");
        File.WriteAllText(jsonPath, JsonSerializer.Serialize(resultados, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"JSON exportado: {jsonPath} ({resultados.Count} linhas)");
    }

    // ------------------------------------------------------------------- v2

    /// <summary>Uma linha da bateria comparativa V1×V2×V2-recorrente — ver <see cref="V2"/>.</summary>
    private sealed record ResultadoComparacaoV2(
        string Variante, int Repeticao, int Populacao, int Geracoes, int TetoComplexidade, bool RedeRecorrente, bool FitnessV2,
        double FitnessTreinoPico, int GeracaoDoPico, double SegundosTreino,
        double ValFitnessMedio, double ValAlvosNascidosMedio, double ValAbatidosMedio, double ValEscapadosMedio,
        double ValTirosErradosMedio, double ValTempoRespostaMedioSegundos,
        double ValTrocasPenalizadasMedia, double ValCooperacoesConcedidasMedia, double ValCooperacoesNegadasMedia);

    /// <summary>
    /// Bateria comparativa real pedida na tarefa: treina o controlador V1 (referência) e o V2
    /// (novas entradas/saída/fitness — ver ControladorHibridoV2 e ParametrosTreinamento), em
    /// condições equivalentes (mesma população, mesmas gerações, mesmos cenários táticos fixos,
    /// mesmas seeds de validação) e com <paramref name="args"/> <c>--repeticoes</c> sementes de
    /// treino diferentes. Grava um JSON por linha (sem sobrescrever execuções anteriores) e
    /// imprime a média por variante — NADA aqui é estimado: cada número vem de rodar o ambiente.
    /// </summary>
    private static void V2(string[] args)
    {
        int populacao = (int)Arg(args, "pop", 24);
        int geracoes = (int)Arg(args, "ger", 40);
        int repeticoes = (int)Arg(args, "repeticoes", 3);
        int episodiosValidacao = (int)Arg(args, "val", 30);
        int tetoComplexidade = (int)Arg(args, "teto", 60);
        bool incluirRecorrente = ArgS(args, "recorrente", "sim") == "sim";

        Console.WriteLine($"--- COMPARACAO V1 x V2 (pop={populacao}, geracoes={geracoes}, repeticoes={repeticoes}, validacao={episodiosValidacao} episodios, teto={tetoComplexidade}) ---");

        var todos = new List<ResultadoComparacaoV2>();

        for (int r = 0; r < repeticoes; r++)
        {
            todos.Add(RodarV1(r, populacao, geracoes, tetoComplexidade, episodiosValidacao));
            todos.Add(RodarV2(r, populacao, geracoes, tetoComplexidade, episodiosValidacao, redeRecorrente: false));
            if (incluirRecorrente)
            {
                todos.Add(RodarV2(r, populacao, geracoes, tetoComplexidade, episodiosValidacao, redeRecorrente: true));
            }
        }

        string caminho = Path.Combine(PastaResultados(), "comparacao_v2.json");
        List<ResultadoComparacaoV2> acumulado = File.Exists(caminho)
            ? JsonSerializer.Deserialize<List<ResultadoComparacaoV2>>(File.ReadAllText(caminho)) ?? new()
            : new();
        acumulado.AddRange(todos);
        File.WriteAllText(caminho, JsonSerializer.Serialize(acumulado, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"JSON exportado (acrescentado): {caminho} ({todos.Count} linhas novas, {acumulado.Count} no total)");

        foreach (var grupo in todos.GroupBy(r => r.Variante))
        {
            List<ResultadoComparacaoV2> linhas = grupo.ToList();
            Console.WriteLine($"[{grupo.Key}] (média de {linhas.Count} execução(ões))");
            Console.WriteLine($"  fitness treino (pico)  : {linhas.Average(l => l.FitnessTreinoPico):0.00}");
            Console.WriteLine($"  VAL fitness médio      : {linhas.Average(l => l.ValFitnessMedio):0.00}");
            Console.WriteLine($"  VAL abatidos médio     : {linhas.Average(l => l.ValAbatidosMedio):0.00}");
            Console.WriteLine($"  VAL escapados médio    : {linhas.Average(l => l.ValEscapadosMedio):0.00}");
            Console.WriteLine($"  VAL tiros errados médio: {linhas.Average(l => l.ValTirosErradosMedio):0.00}");
            Console.WriteLine($"  VAL tempo resposta (s) : {linhas.Average(l => l.ValTempoRespostaMedioSegundos):0.00}");
            Console.WriteLine($"  VAL trocas penalizadas : {linhas.Average(l => l.ValTrocasPenalizadasMedia):0.00}");
            Console.WriteLine($"  VAL cooperacoes (conc/neg): {linhas.Average(l => l.ValCooperacoesConcedidasMedia):0.00} / {linhas.Average(l => l.ValCooperacoesNegadasMedia):0.00}");
            Console.WriteLine($"  tempo de treino (s)    : {linhas.Average(l => l.SegundosTreino):0.0}");
        }
    }

    private static ResultadoComparacaoV2 RodarV1(int repeticao, int populacao, int geracoes, int tetoComplexidade, int episodiosValidacao)
    {
        var parametros = new ParametrosTreinamento { TamanhoPopulacao = populacao, TetoComplexidade = tetoComplexidade };
        var ambiente = new AmbienteTreinamento(parametros);
        var treinador = new TreinadorNeat(ambiente, tamanhoPopulacao: populacao, quantidadeEspecies: parametros.QuantidadeEspecies, tetoComplexidade: tetoComplexidade, probabilidadeAdicionarNo: parametros.ProbabilidadeAdicionarNo);

        var cronometro = Stopwatch.StartNew();
        double melhor = double.NegativeInfinity;
        int geracaoPico = 0;
        NeatGenome<double>? melhorGenoma = null;

        for (int g = 0; g < geracoes; g++)
        {
            int[] seeds = Enumerable.Range(0, Math.Max(1, parametros.EpisodiosPorAvaliacao)).Select(i => (repeticao * 100000) + treinador.GeracaoAtual * 1000 + i).ToArray();
            ResultadoGeracao resultado = treinador.EvoluirGeracao(seeds, CatalogoCenarios.TodosComScript);
            if (resultado.MelhorFitness > melhor) { melhor = resultado.MelhorFitness; geracaoPico = resultado.Geracao; melhorGenoma = resultado.MelhorGenoma; }
        }
        cronometro.Stop();

        RedeNeat rede = melhorGenoma is null ? RedeNula() : treinador.Decodificar(melhorGenoma);
        var ambienteVal = new AmbienteTreinamento(parametros);
        var fit = new List<double>(); var nasc = new List<int>(); var ab = new List<int>(); var esc = new List<int>(); var erros = new List<int>(); var resp = new List<double>();

        for (int s = 95000; s < 95000 + episodiosValidacao; s++)
        {
            EstadoEpisodio estado = ambienteVal.NovoEpisodio(seed: repeticao * 1000 + s);
            var controlador = new ControladorHibrido(rede, parametros.RaioMundoMetros, parametros.VelocidadeMaximaAlvoMetrosPorSegundo);
            const double dt = 0.2;
            while (!estado.Finalizado) ambienteVal.Avancar(estado, controlador, dt);

            fit.Add(estado.FitnessAcumulado); nasc.Add(estado.AlvosNascidos); ab.Add(estado.AlvosAbatidos);
            esc.Add(estado.AlvosEscapados); erros.Add(estado.TirosErrados); resp.Add(estado.TempoDeRespostaMedioSegundos);
        }

        return new ResultadoComparacaoV2(
            "V1-referencia", repeticao, populacao, geracoes, tetoComplexidade, RedeRecorrente: false, FitnessV2: false,
            melhor, geracaoPico, cronometro.Elapsed.TotalSeconds,
            fit.Average(), nasc.Average(), ab.Average(), esc.Average(), erros.Average(), resp.Where(r => r > 0).DefaultIfEmpty(0).Average(),
            0, 0, 0);
    }

    private static ResultadoComparacaoV2 RodarV2(int repeticao, int populacao, int geracoes, int tetoComplexidade, int episodiosValidacao, bool redeRecorrente)
    {
        var parametros = new ParametrosTreinamento { TamanhoPopulacao = populacao, TetoComplexidade = tetoComplexidade, UsarFitnessV2 = true, RedeRecorrente = redeRecorrente };
        var ambiente = new AmbienteTreinamento(parametros);
        var treinador = new TreinadorNeat(
            ambiente, tamanhoPopulacao: populacao, quantidadeEspecies: parametros.QuantidadeEspecies, tetoComplexidade: tetoComplexidade,
            probabilidadeAdicionarNo: parametros.ProbabilidadeAdicionarNo, controladorV2: true, redeRecorrente: redeRecorrente);

        var cronometro = Stopwatch.StartNew();
        double melhor = double.NegativeInfinity;
        int geracaoPico = 0;
        NeatGenome<double>? melhorGenoma = null;

        for (int g = 0; g < geracoes; g++)
        {
            int[] seeds = Enumerable.Range(0, Math.Max(1, parametros.EpisodiosPorAvaliacao)).Select(i => (repeticao * 100000) + treinador.GeracaoAtual * 1000 + i).ToArray();
            ResultadoGeracao resultado = treinador.EvoluirGeracao(seeds, CatalogoCenarios.TodosComScript);
            if (resultado.MelhorFitness > melhor) { melhor = resultado.MelhorFitness; geracaoPico = resultado.Geracao; melhorGenoma = resultado.MelhorGenoma; }
        }
        cronometro.Stop();

        var ambienteVal = new AmbienteTreinamento(parametros);
        var fit = new List<double>(); var nasc = new List<int>(); var ab = new List<int>(); var esc = new List<int>(); var erros = new List<int>(); var resp = new List<double>();
        var trocas = new List<int>(); var coopConc = new List<int>(); var coopNeg = new List<int>();

        // Rede única por configuração: estável (sem estado) usa Decodificar; recorrente usa
        // CriarRedeRecorrente (estado isolado por torre, reiniciado a cada episódio novo dentro de
        // ExecutarEpisodioRapidoV2 — ver RedeNeatRecorrente).
        RedeNeat? redeEstavel = redeRecorrente ? null : (melhorGenoma is null ? RedeNula() : treinador.Decodificar(melhorGenoma));
        RedeNeatRecorrente? redeRecorrenteInstancia = redeRecorrente && melhorGenoma is not null ? treinador.CriarRedeRecorrente(melhorGenoma) : null;

        for (int s = 95000; s < 95000 + episodiosValidacao; s++)
        {
            int seedEpisodio = repeticao * 1000 + s;

            EpisodioResultado resultadoEpisodio = redeRecorrenteInstancia is not null
                ? ambienteVal.ExecutarEpisodioRapidoV2(redeRecorrenteInstancia, seedEpisodio)
                : ambienteVal.ExecutarEpisodioRapidoV2(redeEstavel!, seedEpisodio);

            fit.Add(resultadoEpisodio.FitnessTotal); nasc.Add(resultadoEpisodio.AlvosNascidos); ab.Add(resultadoEpisodio.AlvosAbatidos);
            esc.Add(resultadoEpisodio.AlvosEscapados); erros.Add(resultadoEpisodio.TirosErrados);
            if (resultadoEpisodio.TempoDeRespostaMedioSegundos > 0) resp.Add(resultadoEpisodio.TempoDeRespostaMedioSegundos);
            trocas.Add(resultadoEpisodio.TrocasDeAlvoPenalizadas);
            coopConc.Add(resultadoEpisodio.CooperacoesConcedidas);
            coopNeg.Add(resultadoEpisodio.CooperacoesNegadasPorConcentracao);
        }

        return new ResultadoComparacaoV2(
            redeRecorrente ? "V2-recorrente" : "V2", repeticao, populacao, geracoes, tetoComplexidade, redeRecorrente, FitnessV2: true,
            melhor, geracaoPico, cronometro.Elapsed.TotalSeconds,
            fit.Average(), nasc.Average(), ab.Average(), esc.Average(), erros.Average(), resp.DefaultIfEmpty(0).Average(),
            trocas.Average(), coopConc.Average(), coopNeg.Average());
    }

    // ----------------------------------------------------------- resultados

    private static string PastaResultados()
    {
        string? raiz = AppContext.BaseDirectory;
        while (raiz is not null && !Directory.Exists(Path.Combine(raiz, "tools", "RadarTorres.Experimentos")))
            raiz = Path.GetDirectoryName(raiz);
        string pasta = Path.Combine(raiz ?? ".", "tools", "RadarTorres.Experimentos", "Resultados");
        Directory.CreateDirectory(pasta);
        return pasta;
    }

    /// <summary>
    /// Acrescenta a execução a resultados.json — arquivo próprio deste executor. A tabela
    /// histórica experimentos_hiperparametros do SQLite não é tocada.
    /// </summary>
    private static void Gravar(Execucao r)
    {
        string caminho = Path.Combine(PastaResultados(), "resultados.json");
        List<Execucao> todas = File.Exists(caminho)
            ? JsonSerializer.Deserialize<List<Execucao>>(File.ReadAllText(caminho)) ?? new List<Execucao>()
            : new List<Execucao>();
        todas.Add(r);
        File.WriteAllText(caminho, JsonSerializer.Serialize(todas, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Grava a rede campeã no formato <c>.net</c> do SharpNEAT. Substitui o JSON com arquitetura
    /// + vetor de pesos, que não representa uma topologia evoluída; o fitness não cabe mais no
    /// arquivo (o formato não tem metadados) e continua registrado em <c>resultados.json</c>.
    /// </summary>
    private static void SalvarRede(string rotulo, NeatGenome<double> genoma)
        => NeatGenomeSaver.Save(genoma, Path.Combine(PastaResultados(), $"rede-{rotulo}.net"));
}
