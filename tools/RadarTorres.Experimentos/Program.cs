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
            case "comparacao": Comparacao(args); return 0;
            case "dificuldade": Dificuldade(args); return 0;
            default:
                Console.WriteLine("Comandos: teto | ga | ablacao | hiperparametros | sobrevivencia | comparacao | dificuldade");
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
        int maxAlvos = (int)Arg(args, "maxalvos", 5);
        int episodios = (int)Arg(args, "episodios", 200);
        var p = new ParametrosTreinamento
        {
            MaxAlvosPorQuadrante = maxAlvos,
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

        Console.WriteLine($"--- TETO (MaxAlvosPorQuadrante={maxAlvos}, duracao={p.DuracaoEpisodioSegundos}s, spawn={p.IntervaloSpawnMinSegundos}-{p.IntervaloSpawnMaxSegundos}s, {episodios} episodios, seeds 0..{episodios - 1}) ---");
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
            MaxAlvosPorQuadrante = maxAlvos,
            DuracaoEpisodioSegundos = duracao,
            IntervaloSpawnMinSegundos = spawnMin,
            IntervaloSpawnMaxSegundos = spawnMax,
            SemLimiteDeDuracao = semLimiteDeDuracao
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
        var parametros = new ParametrosTreinamento { MaxAlvosPorQuadrante = maxAlvos };
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
    /// tela "Treinamento do Controlador") e avalia quanto ele realmente
    /// aguenta quando o episódio não é mais cortado em <see cref="ParametrosTreinamento.DuracaoEpisodioSegundos"/>
    /// -- só termina quando um alvo de fato rompe uma torre
    /// (<see cref="ParametrosTreinamento.SemLimiteDeDuracao"/>). Responde diretamente à pergunta
    /// "o teto é da rede ou do ambiente?": se o fitness médio aqui for muito maior que o teto
    /// observado com episódios de 45 s, o ambiente (duração curta) é o limitador, não o genoma.
    /// </summary>
    private static void Sobrevivencia(string[] args)
    {
        string caminho = ArgS(args, "genoma", "");
        if (caminho.Length == 0 || !File.Exists(caminho))
        {
            Console.WriteLine("Uso: sobrevivencia --genoma=<caminho.net> [--episodios=20] [--semlimite=sim]");
            return;
        }

        RedeNeat redeCarregada = CarregarRede(caminho);
        int episodios = (int)Arg(args, "episodios", 20);
        bool semLimite = ArgS(args, "semlimite", "sim") == "sim";
        var modo = Enum.Parse<ControladorHibrido.ModoDecisao>(ArgS(args, "modo", "Hibrido"));

        var parametros = new ParametrosTreinamento { SemLimiteDeDuracao = semLimite };
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

        Console.WriteLine($"--- SOBREVIVENCIA ({episodios} episodios, semlimite={semLimite}, modo={modo}, genoma de {caminho}) ---");
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

    // ------------------------------------------- comparacao pareada (protocolo do artigo)

    /// <summary>Primeira seed do conjunto de TESTE: acima de qualquer seed de treino (geração×1000+i, com i &lt; 12) para até 4999 gerações, e fora das seeds 90000..90049 de <see cref="RodarGa"/>, que colidem com as da geração 90.</summary>
    private const int BaseSeedsTeste = 5_000_000;

    private sealed record ResumoComparacao(
        string Rotulo, string Modo, int Execucao, int Geracoes, double MelhorFitnessTreino, int GeracaoDoPico,
        double SegundosTreino, int Conexoes, double Media, double Desvio, double IcInf, double IcSup,
        double DifMediaVsHeuristica, double DifIcInf, double DifIcSup, double WilcoxonZ, double WilcoxonP,
        int Oportunidades, int Recusados, double TaxaRecusa, double[] FitnessPorSeed);

    /// <summary>
    /// Protocolo de avaliação do artigo: treina <c>--execucoes</c> controladores independentes por
    /// modo (Hibrido e RedePura) com os parâmetros de fábrica de <see cref="ParametrosTreinamento"/>
    /// (os mesmos da tela de treinamento), escolhe o campeão de cada execução SÓ pelo fitness de
    /// treino e o avalia uma única vez em <c>--teste</c> seeds reservadas (nunca usadas no treino nem
    /// na escolha do campeão). A heurística pura é avaliada nas MESMAS seeds, o que permite teste
    /// pareado seed a seed (Wilcoxon) e IC 95% da diferença.
    /// </summary>
    private static void Comparacao(string[] args)
    {
        int execucoes = (int)Arg(args, "execucoes", 5);
        int geracoes = (int)Arg(args, "ger", 1000);
        int nTeste = (int)Arg(args, "teste", 200);
        int[] seedsTeste = Enumerable.Range(BaseSeedsTeste, nTeste).ToArray();

        var parametros = new ParametrosTreinamento();
        var ambienteTeste = new AmbienteTreinamento(parametros);
        RedeNeat nulo = RedeNula();

        double[] heur = seedsTeste
            .Select(s => ambienteTeste.ExecutarEpisodioRapido(nulo, s, TipoCenarioTreinamento.Aleatorio, ControladorHibrido.ModoDecisao.HeuristicaPura).FitnessTotal)
            .ToArray();
        var resumos = new List<ResumoComparacao>
        {
            Resumir("heuristica", "HeuristicaPura", 0, 0, 0, 0, 0, 0, heur, heur, 0, 0)
        };
        Console.WriteLine($"[heuristica] media={heur.Average():0.00} desvio={Desvio(heur):0.00} (n={nTeste})");

        foreach (ControladorHibrido.ModoDecisao modo in new[] { ControladorHibrido.ModoDecisao.Hibrido, ControladorHibrido.ModoDecisao.RedePura })
        {
            for (int e = 1; e <= execucoes; e++)
            {
                var ambiente = new AmbienteTreinamento(parametros);
                var ag = new TreinadorNeat(
                    ambiente,
                    tamanhoPopulacao: parametros.TamanhoPopulacao,
                    quantidadeEspecies: parametros.QuantidadeEspecies,
                    tetoComplexidade: parametros.TetoComplexidade,
                    probabilidadeAdicionarNo: parametros.ProbabilidadeAdicionarNo);

                var cronometro = Stopwatch.StartNew();
                double melhor = double.NegativeInfinity;
                int geracaoPico = 0;
                NeatGenome<double>? campeao = null;
                for (int g = 0; g < geracoes; g++)
                {
                    int[] seeds = Enumerable.Range(0, parametros.EpisodiosPorAvaliacao).Select(i => ag.GeracaoAtual * 1000 + i).ToArray();
                    ResultadoGeracao r = ag.EvoluirGeracao(seeds, null, modo);
                    if (r.MelhorFitness > melhor)
                    {
                        melhor = r.MelhorFitness;
                        geracaoPico = r.Geracao;
                        campeao = r.MelhorGenoma;
                    }
                }
                cronometro.Stop();

                RedeNeat rede = campeao is null ? nulo : ag.Decodificar(campeao);
                var fit = new double[nTeste];
                int oportunidades = 0, recusados = 0;
                for (int i = 0; i < nTeste; i++)
                {
                    EpisodioResultado res = ambienteTeste.ExecutarEpisodioRapido(rede, seedsTeste[i], TipoCenarioTreinamento.Aleatorio, modo);
                    fit[i] = res.FitnessTotal;
                    oportunidades += res.OportunidadesDeDisparo;
                    recusados += res.DisparosRecusadosPelaRede;
                }

                string rotulo = $"cmp-{modo}-e{e}";
                ResumoComparacao resumo = Resumir(rotulo, modo.ToString(), e, geracoes, melhor, geracaoPico,
                    cronometro.Elapsed.TotalSeconds, campeao?.ConnectionGenes.Length ?? 0, fit, heur, oportunidades, recusados);
                resumos.Add(resumo);
                if (campeao is not null) SalvarRede(rotulo, campeao);
                Console.WriteLine($"[{rotulo}] treino: pico={melhor:0.00} (g{geracaoPico}) {cronometro.Elapsed.TotalSeconds:0}s conexoes={resumo.Conexoes} | TESTE media={resumo.Media:0.00} IC95=[{resumo.IcInf:0.00};{resumo.IcSup:0.00}] dif vs heur={resumo.DifMediaVsHeuristica:+0.00;-0.00} [{resumo.DifIcInf:0.00};{resumo.DifIcSup:0.00}] Wilcoxon p={resumo.WilcoxonP:0.0000} recusa={resumo.TaxaRecusa:P1}");
            }
        }

        // Comparação entre modos: média por seed sobre as execuções de cada modo, pareada por seed.
        double[] MediaPorSeed(string m) => Enumerable.Range(0, nTeste)
            .Select(i => resumos.Where(r => r.Modo == m).Average(r => r.FitnessPorSeed[i])).ToArray();
        double[] hib = MediaPorSeed("Hibrido");
        double[] pura = MediaPorSeed("RedePura");
        var entreModos = new[]
        {
            Resumir("agregado-Hibrido", "Hibrido(media execucoes)", 0, geracoes, 0, 0, 0, 0, hib, heur, 0, 0),
            Resumir("agregado-RedePura", "RedePura(media execucoes)", 0, geracoes, 0, 0, 0, 0, pura, heur, 0, 0),
            Resumir("RedePura-vs-Hibrido", "RedePura-Hibrido", 0, geracoes, 0, 0, 0, 0, pura, hib, 0, 0),
        };
        resumos.AddRange(entreModos);
        foreach (ResumoComparacao r in entreModos)
        {
            Console.WriteLine($"[{r.Rotulo}] media={r.Media:0.00} dif={r.DifMediaVsHeuristica:+0.00;-0.00} IC95=[{r.DifIcInf:0.00};{r.DifIcSup:0.00}] Wilcoxon z={r.WilcoxonZ:0.00} p={r.WilcoxonP:0.0000}");
        }

        string caminho = Path.Combine(PastaResultados(), "comparacao_pareada.json");
        File.WriteAllText(caminho, JsonSerializer.Serialize(new
        {
            Protocolo = new
            {
                Execucoes = execucoes, Geracoes = geracoes, SeedsTeste = $"{BaseSeedsTeste}..{BaseSeedsTeste + nTeste - 1}",
                parametros.TamanhoPopulacao, parametros.TetoComplexidade, parametros.ProbabilidadeAdicionarNo,
                parametros.EpisodiosPorAvaliacao, parametros.DuracaoEpisodioSegundos, parametros.PenalidadeRecusaDisparo,
                Maquina = Environment.MachineName, Nucleos = Environment.ProcessorCount,
                Data = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
            },
            Resultados = resumos
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"JSON: {caminho}");
    }

    /// <summary>
    /// Diagnóstico da grade de hiperparâmetros: mede, com um controlador FIXO (heurística pura), a
    /// dificuldade do conjunto de seeds de cada geração (as mesmas para toda configuração da grade).
    /// Se a geração de pico da grade coincidir com a geração de seeds mais fáceis, o "pico" mede o
    /// sorteio de cenários, não a configuração.
    /// </summary>
    private static void Dificuldade(string[] args)
    {
        int geracoes = (int)Arg(args, "ger", 300);
        var parametros = new ParametrosTreinamento();
        var amb = new AmbienteTreinamento(parametros);
        RedeNeat nulo = RedeNula();
        var porGeracao = new List<(int Geracao, double Fitness)>();
        for (int g = 0; g < geracoes; g++)
        {
            int[] seeds = Enumerable.Range(0, parametros.EpisodiosPorAvaliacao).Select(i => g * 1000 + i).ToArray();
            porGeracao.Add((g, amb.AvaliarFitness(nulo, seeds, CatalogoCenarios.TodosComScript, ControladorHibrido.ModoDecisao.HeuristicaPura)));
        }
        var ordenado = porGeracao.OrderByDescending(x => x.Fitness).ToList();
        int posicao222 = ordenado.FindIndex(x => x.Geracao == 222) + 1;
        Console.WriteLine($"--- DIFICULDADE das seeds por geracao (heuristica pura, {geracoes} geracoes) ---");
        Console.WriteLine($"media={porGeracao.Average(x => x.Fitness):0.00} desvio={Desvio(porGeracao.Select(x => x.Fitness).ToArray()):0.00} min={porGeracao.Min(x => x.Fitness):0.00} max={porGeracao.Max(x => x.Fitness):0.00}");
        Console.WriteLine("5 geracoes com seeds mais faceis: " + string.Join(", ", ordenado.Take(5).Select(x => $"g{x.Geracao}={x.Fitness:0.00}")));
        Console.WriteLine($"geracao 222: fitness={porGeracao[Math.Min(222, geracoes - 1)].Fitness:0.00}, posicao {posicao222} de {geracoes}");
        File.WriteAllText(Path.Combine(PastaResultados(), "dificuldade_seeds_por_geracao.json"),
            JsonSerializer.Serialize(porGeracao.Select(x => new { x.Geracao, x.Fitness }), new JsonSerializerOptions { WriteIndented = true }));
    }

    private static ResumoComparacao Resumir(string rotulo, string modo, int execucao, int geracoes, double melhor, int pico,
        double segundos, int conexoes, double[] fit, double[] referencia, int oportunidades, int recusados)
    {
        double[] dif = fit.Zip(referencia, (a, b) => a - b).ToArray();
        (double icInf, double icSup) = IntervaloConfianca95(fit);
        (double difInf, double difSup) = IntervaloConfianca95(dif);
        (double z, double p) = Wilcoxon(dif);
        return new ResumoComparacao(rotulo, modo, execucao, geracoes, melhor, pico, segundos, conexoes,
            fit.Average(), Desvio(fit), icInf, icSup, dif.Average(), difInf, difSup, z, p,
            oportunidades, recusados, oportunidades > 0 ? (double)recusados / oportunidades : 0, fit);
    }

    /// <summary>Desvio padrão amostral (n-1).</summary>
    private static double Desvio(double[] x)
    {
        if (x.Length < 2) return 0;
        double m = x.Average();
        return Math.Sqrt(x.Sum(v => (v - m) * (v - m)) / (x.Length - 1));
    }

    /// <summary>IC 95% da média pela distribuição t (n &gt;= 30 usa 1,96).</summary>
    private static (double, double) IntervaloConfianca95(double[] x)
    {
        double[] t = [12.706, 4.303, 3.182, 2.776, 2.571, 2.447, 2.365, 2.306, 2.262, 2.228, 2.201, 2.179, 2.160, 2.145, 2.131, 2.120, 2.110, 2.101, 2.093, 2.086, 2.080, 2.074, 2.069, 2.064, 2.060, 2.056, 2.052, 2.048, 2.045];
        int gl = x.Length - 1;
        if (gl < 1) return (x.Average(), x.Average());
        double crit = gl <= t.Length ? t[gl - 1] : 1.96;
        double meia = crit * Desvio(x) / Math.Sqrt(x.Length);
        return (x.Average() - meia, x.Average() + meia);
    }

    /// <summary>Teste de postos sinalizados de Wilcoxon (bicaudal), aproximação normal com correção de empates e de continuidade; diferenças nulas são descartadas.</summary>
    private static (double Z, double P) Wilcoxon(double[] dif)
    {
        double[] d = dif.Where(v => Math.Abs(v) > 1e-9).ToArray();
        int n = d.Length;
        if (n == 0) return (0, 1);
        var ordem = d.Select((v, i) => (Abs: Math.Abs(v), I: i)).OrderBy(x => x.Abs).ToArray();
        var postos = new double[n];
        double correcaoEmpates = 0;
        for (int i = 0; i < n;)
        {
            int j = i;
            while (j + 1 < n && Math.Abs(ordem[j + 1].Abs - ordem[i].Abs) < 1e-9) j++;
            double posto = (i + j + 2) / 2.0;
            for (int k = i; k <= j; k++) postos[ordem[k].I] = posto;
            int tamanho = j - i + 1;
            correcaoEmpates += tamanho * tamanho * tamanho - tamanho;
            i = j + 1;
        }
        double wMais = d.Select((v, i) => v > 0 ? postos[i] : 0).Sum();
        double media = n * (n + 1) / 4.0;
        double variancia = n * (n + 1) * (2 * n + 1) / 24.0 - correcaoEmpates / 48.0;
        if (variancia <= 0) return (0, 1);
        double z = (wMais - media - Math.Sign(wMais - media) * 0.5) / Math.Sqrt(variancia);
        double p = 2 * (1 - Phi(Math.Abs(z)));
        return (z, Math.Clamp(p, 0, 1));
    }

    /// <summary>CDF da normal padrão (Abramowitz e Stegun 7.1.26, erro &lt; 1,5e-7).</summary>
    private static double Phi(double x)
    {
        double t = 1 / (1 + 0.3275911 * Math.Abs(x) / Math.Sqrt(2));
        double erf = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x / 2);
        return x >= 0 ? 0.5 * (1 + erf) : 0.5 * (1 - erf);
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
