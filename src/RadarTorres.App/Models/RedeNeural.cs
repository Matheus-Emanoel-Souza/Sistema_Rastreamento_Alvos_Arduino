using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Registro de uma versão/variação da rede neural do <c>ControladorHibrido</c> efetivamente
/// implementada ou testada no projeto — identificador, contrato de entradas/saídas, tipo de
/// camada oculta e biblioteca usada (ver <see cref="Repositories.IRedeNeuralRepository"/>).
/// Existe para dar rastreabilidade ao histórico de redesenhos do controlador (análogo em espírito
/// a <see cref="ExperimentoHiperparametros"/>, mas no nível de ARQUITETURA da rede, não de
/// hiperparâmetro de treino) — cada linha aqui é uma versão real do código em algum ponto do
/// histórico, nunca uma arquitetura hipotética.
/// </summary>
public sealed class RedeNeural
{
    public int Id { get; set; }

    /// <summary>
    /// Identificador de versão em texto (não inteiro) de propósito: comporta variações da mesma
    /// versão numérica, como "3b" para a variante recorrente de uma rede cuja versão base é "3" —
    /// ver <see cref="TipoCamadaOculta"/> e a nota de <see cref="Observacoes"/> de cada linha para
    /// o que distingue cada uma.
    /// </summary>
    public string Versao { get; set; } = string.Empty;

    /// <summary>Nome/classe que implementa esta versão (ex.: "ControladorHibrido", "ControladorHibridoV2").</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Quantidade de entradas da rede. <c>null</c> quando a versão está reservada e ainda não foi
    /// recuperada/conferida (ver versão "1") — não inventa um número antes de confirmar no código-fonte real.
    /// </summary>
    public int? Entradas { get; set; }

    /// <summary>Quantidade de saídas da rede. <c>null</c> nas mesmas condições de <see cref="Entradas"/>.</summary>
    public int? Saidas { get; set; }

    /// <summary><see cref="TipoCamadaOcultaFixa"/> ou <see cref="TipoCamadaOcultaVariavel"/>. <c>null</c> se ainda não confirmado.</summary>
    public string? TipoCamadaOculta { get; set; }

    public const string TipoCamadaOcultaFixa = "Fixa";
    public const string TipoCamadaOcultaVariavel = "Variavel";

    /// <summary>
    /// Quantidade de neurônios ocultos — só preenchido quando <see cref="TipoCamadaOculta"/> é
    /// <see cref="TipoCamadaOcultaFixa"/>. <c>null</c> quando a camada é de tamanho variável (o
    /// NEAT evolui a topologia; a contagem de neurônios ocultos do campeão é RESULTADO do treino,
    /// não parâmetro da versão) ou quando ainda não confirmado.
    /// </summary>
    public int? NeuroniosOcultos { get; set; }

    /// <summary>Biblioteca/framework usado (ex.: "SharpNEAT 4.1.0", "implementação própria (sem biblioteca)"). <c>null</c> se não confirmado.</summary>
    public string? Biblioteca { get; set; }

    public string? Observacoes { get; set; }

    public DateTime DataRegistro { get; set; }
}
