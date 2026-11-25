using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Uma execução de treinamento (neuroevolução): tudo que acontece entre um clique em "Iniciar" e
/// a parada correspondente na tela "Treinamento do Controlador".
/// </summary>
/// <remarks>
/// Até esta tabela existir, <c>ExecucaoId</c> era um Guid solto repetido em
/// <see cref="HistoricoGeracaoTreinamento"/>, <see cref="MarcoFitnessTreinamento"/>,
/// <see cref="InstantaneoComportamentoMelhorGene"/> e <see cref="EventoTreinamento"/> — sem
/// nenhuma linha que dissesse QUANDO aquela execução começou, com quais hiperparâmetros e como
/// terminou. Essas quatro tabelas apareciam isoladas no diagrama do banco justamente por isso.
/// Aqui a execução vira a entidade pai dessas quatro (e de <see cref="EspelhoSistema"/>).
/// </remarks>
public sealed class ExecucaoTreinamento
{
    /// <summary>Guid "N" gerado a cada "Iniciar" — mesma chave já gravada nas tabelas filhas.</summary>
    public string ExecucaoId { get; set; } = string.Empty;

    public DateTime DataHoraInicio { get; set; }

    /// <summary>NULL enquanto a execução está em andamento. É o que distingue a execução ativa.</summary>
    public DateTime? DataHoraFim { get; set; }

    public int TamanhoPopulacao { get; set; }
    public int QuantidadeEspecies { get; set; }
    public int TetoComplexidade { get; set; }

    /// <summary>Meta de gerações pedida na tela; 0 quando a execução roda sem meta.</summary>
    public int MetaGeracoes { get; set; }

    /// <summary>
    /// Mesma descrição gravada em <see cref="MarcoFitnessTreinamento.ConfiguracaoFitness"/>
    /// (ver <c>ParametrosTreinamento.DescricaoConfiguracaoFitness</c>) — repetida aqui para
    /// comparar execuções inteiras sem precisar abrir os marcos de cada uma.
    /// </summary>
    public string ConfiguracaoFitness { get; set; } = "desconhecida";

    /// <summary>Gerações concluídas até o encerramento; 0 enquanto ninguém atualizou o fechamento.</summary>
    public int GeracoesConcluidas { get; set; }

    /// <summary>Melhor fitness alcançado na execução; NULL enquanto nenhuma geração terminou.</summary>
    public double? MelhorFitness { get; set; }

    public EncerramentoExecucaoTreinamento Encerramento { get; set; } = EncerramentoExecucaoTreinamento.EmAndamento;
}

/// <summary>Como uma <see cref="ExecucaoTreinamento"/> terminou.</summary>
public enum EncerramentoExecucaoTreinamento
{
    /// <summary>Ainda rodando — <see cref="ExecucaoTreinamento.DataHoraFim"/> é NULL.</summary>
    EmAndamento,

    /// <summary>Usuário clicou em "Parar".</summary>
    ParadaPeloUsuario,

    /// <summary>A meta de gerações configurada foi atingida e o laço encerrou sozinho.</summary>
    MetaAtingida,

    /// <summary>
    /// Nunca foi fechada: o aplicativo encerrou (ou caiu) com o treinamento rodando. Atribuído na
    /// abertura seguinte do banco, não no momento da queda.
    /// </summary>
    Interrompida,
}
