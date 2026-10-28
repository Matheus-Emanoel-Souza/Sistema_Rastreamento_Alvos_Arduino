using System;
using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.NeuralEvolution;

/// <summary>
/// Estado mutável de um episódio de treinamento em andamento — torres, alvos vivos, relógio
/// interno e estatísticas acumuladas. Uma mesma instância é avançada passo a passo por
/// <see cref="AmbienteTreinamento.Avancar"/>, seja numa avaliação rápida (sem desenhar nada,
/// para o algoritmo genético) ou numa reprodução visual lenta (tela de treinamento).
/// </summary>
public sealed class EstadoEpisodio
{
    public List<TorreTreinamento> Torres { get; } = new();
    public List<AlvoTreinamento> Alvos { get; } = new();

    public double TempoDecorridoSegundos { get; set; }
    public double FitnessAcumulado { get; set; }

    public int AlvosAbatidos { get; set; }
    public int AlvosEscapados { get; set; }

    /// <summary>Disparos autorizados pela heurística/rede que erraram por sorte (ver CalculadoraErroDisparo) — distinto de fuga (nem chegou a disparar) e de abate.</summary>
    public int TirosErrados { get; set; }

    /// <summary>
    /// Quantas vezes os vetos inegociáveis (zona morta, certeza mínima) já liberaram o disparo e
    /// só restava a decisão de <see cref="ControladorHibrido.DecidirDisparo"/> — a heurística pura
    /// dispararia em TODAS essas oportunidades; conta quantas delas, de fato, chegam a esse ponto
    /// (ver <see cref="DisparosRecusadosPelaRede"/> para quantas são recusadas).
    /// </summary>
    public int OportunidadesDeDisparo { get; set; }

    /// <summary>Dentre <see cref="OportunidadesDeDisparo"/>, quantas a rede recusou (<c>confiancaDisparo &lt;= 0</c>) — mede diretamente a hipótese de que a rede hesita em disparar onde a heurística pura não hesitaria.</summary>
    public int DisparosRecusadosPelaRede { get; set; }

    public bool Finalizado { get; set; }

    /// <summary>Instante (em <see cref="TempoDecorridoSegundos"/>) do próximo surgimento de alvo.</summary>
    public double ProximoSpawnSegundos { get; set; }

    /// <summary>Gerador de números aleatórios próprio do episódio — permite reproduzir o mesmo episódio com uma seed fixa.</summary>
    public Random Rng { get; }

    /// <summary>Script de nascimentos deste episódio (ver <see cref="CatalogoCenarios"/>), ou <c>null</c> para o sorteio padrão.</summary>
    public IReadOnlyList<SpawnScriptado>? SpawnsScriptados { get; set; }

    /// <summary>Próximo nascimento ainda não disparado em <see cref="SpawnsScriptados"/>.</summary>
    public int ProximoIndiceSpawnScriptado { get; set; }

    /// <summary>Quadrante tratado como zona morta só neste episódio, além dos configurados em <see cref="ParametrosTreinamento.ZonasMortasQuadrantes"/> — ver <see cref="CatalogoCenarios.QuadranteZonaMortaParaForcar"/>.</summary>
    public Quadrant? ZonaMortaForcadaQuadrante { get; set; }

    private int _proximoIdAlvo;

    public EstadoEpisodio(Random rng)
    {
        Rng = rng;
    }

    public int ProximoIdAlvo() => ++_proximoIdAlvo;

    /// <summary>Quantos alvos nasceram neste episódio (ids são sequenciais e nunca reutilizados) — métrica de denominador para taxa de abate, não influencia o fitness.</summary>
    public int AlvosNascidos => _proximoIdAlvo;
}
