using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RadarTorres.App.Configuration;
using RadarTorres.App.NeuralEvolution;

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
            default:
                Console.WriteLine("Comandos: teto | ga | ablacao | hiperparametros");
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

        // Genoma nulo => rede devolve tanh(0)=0 em tudo => confiança de disparo nunca > 0, ou
        // seja, controlador passivo: nenhum abate. Serve para contar nascimentos no pior caso
        // (teto de simultâneos sempre ocupado).
        var nulo = new double[RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)];

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
            Arg(args, "spawnmax", 4.0)));
    }

    private static Execucao RodarGa(
        string rotulo, int populacao, int ocultos, double mutacao, int geracoes, int seed,
        int maxAlvos, ControladorHibrido.ModoDecisao modo, bool cenariosFixos, double limiteSegundos,
        double duracao = 45, double spawnMin = 1.5, double spawnMax = 4.0)
    {
        int[] arquitetura = [ControladorHibrido.QuantidadeEntradas, ocultos, ControladorHibrido.QuantidadeSaidas];
        var parametros = new ParametrosTreinamento
        {
            TamanhoPopulacao = populacao,
            TaxaMutacao = mutacao,
            MaxAlvosPorQuadrante = maxAlvos,
            DuracaoEpisodioSegundos = duracao,
            IntervaloSpawnMinSegundos = spawnMin,
            IntervaloSpawnMaxSegundos = spawnMax
        };
        var ambiente = new AmbienteTreinamento(parametros, arquitetura: arquitetura);
        var ag = new AlgoritmoGenetico(
            tamanhoGenoma: RedeNeural.TamanhoGenoma(arquitetura),
            tamanhoPopulacao: populacao,
            taxaMutacao: mutacao,
            elitismo: Math.Max(2, populacao / 10),
            seed: seed);

        IReadOnlyList<TipoCenarioTreinamento>? fixos = cenariosFixos ? CatalogoCenarios.TodosComScript : null;

        var cronometro = Stopwatch.StartNew();
        double melhor = double.NegativeInfinity;
        int geracaoPico = 0;
        double[] melhorGenoma = new double[RedeNeural.TamanhoGenoma(arquitetura)];
        ResultadoGeracao? ultimo = null;
        int geracoesRodadas = 0;

        for (int g = 0; g < geracoes; g++)
        {
            int[] seeds = Enumerable.Range(0, Math.Max(1, parametros.EpisodiosPorAvaliacao))
                .Select(i => ag.GeracaoAtual * 1000 + i)
                .ToArray();

            ultimo = ag.EvoluirGeracao(genoma => ambiente.AvaliarFitness(genoma, seeds, fixos, modo));
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
        var ambienteVal = new AmbienteTreinamento(parametros, arquitetura: arquitetura);
        var fit = new List<double>();
        var nasc = new List<int>();
        var ab = new List<int>();
        var esc = new List<int>();
        for (int s = 90000; s < 90050; s++)
        {
            EpisodioResultado e = ambienteVal.ExecutarEpisodioRapido(melhorGenoma, s, TipoCenarioTreinamento.Aleatorio, modo);
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

        SalvarGenoma(rotulo, arquitetura, melhorGenoma, melhor);
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
        // seeds de validação, com genoma nulo.
        var parametros = new ParametrosTreinamento { MaxAlvosPorQuadrante = maxAlvos };
        var amb = new AmbienteTreinamento(parametros);
        var nulo = new double[RedeNeural.TamanhoGenoma(ControladorHibrido.ArquiteturaPadrao)];
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

        Console.WriteLine($"Configurações planejadas: {planejadas.Count} (arquitetura: {ControladorHibrido.QuantidadeEntradas} entradas, {ControladorHibrido.QuantidadeSaidas} saídas)");

        var resultados = new List<ResultadoHiperparametro>();
        int indice = 0;

        foreach (ConfiguracaoHiperparametro cfg in planejadas)
        {
            indice++;
            int[] arquitetura = [ControladorHibrido.QuantidadeEntradas, cfg.Ocultos, ControladorHibrido.QuantidadeSaidas];
            var parametros = new ParametrosTreinamento { TamanhoPopulacao = cfg.Populacao, TaxaMutacao = cfg.Mutacao };
            var ambiente = new AmbienteTreinamento(parametros, arquitetura: arquitetura);
            var ag = new AlgoritmoGenetico(
                tamanhoGenoma: RedeNeural.TamanhoGenoma(arquitetura),
                tamanhoPopulacao: cfg.Populacao,
                taxaMutacao: cfg.Mutacao,
                elitismo: Math.Max(2, cfg.Populacao / 10));

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

                ultimo = ag.EvoluirGeracao(genoma => ambiente.AvaliarFitness(genoma, seeds, CatalogoCenarios.TodosComScript));

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

    private static void SalvarGenoma(string rotulo, int[] arquitetura, double[] genoma, double fitness)
    {
        File.WriteAllText(
            Path.Combine(PastaResultados(), $"genoma-{rotulo}.json"),
            JsonSerializer.Serialize(new { rotulo, arquitetura, fitness, genoma }));
    }
}
