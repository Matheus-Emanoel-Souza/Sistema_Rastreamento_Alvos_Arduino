using System;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Dados de uma rodada de detecção por câmera, agregados pelo relatório de desempenho.
/// </summary>
/// <param name="PerfilAtivo">Nome do perfil de alvo em vigor.</param>
/// <param name="LarguraQuadro">Largura do quadro da câmera, em pixels.</param>
/// <param name="AlturaQuadro">Altura do quadro da câmera, em pixels.</param>
/// <param name="Tempos">Custo de cada etapa.</param>
/// <param name="CandidatosPorCor">Regiões propostas por segmentação de cor.</param>
/// <param name="CandidatosPorDetector">Regiões propostas pelo modelo ONNX.</param>
/// <param name="Identificado">Se o alvo foi reconhecido nesta rodada.</param>
/// <param name="MelhorPontuacao">Maior similaridade (ou fração de cor) encontrada no quadro.</param>
/// <param name="DistanciaMetros">Distância estimada, quando identificado.</param>
/// <param name="TempoAteIdentificarMs">
/// Quanto tempo o alvo passou sem ser reconhecido antes desta rodada identificá-lo — ou seja,
/// a latência real de aquisição percebida pelo operador. Só é preenchido na rodada em que o
/// alvo é reconhecido depois de um período sem reconhecimento.
/// </param>
public sealed record RegistroRodada(
    string PerfilAtivo,
    int LarguraQuadro,
    int AlturaQuadro,
    TemposRodada Tempos,
    int CandidatosPorCor,
    int CandidatosPorDetector,
    bool Identificado,
    double MelhorPontuacao,
    double? DistanciaMetros,
    double? TempoAteIdentificarMs);

/// <summary>
/// Relatório de desempenho da detecção por câmera: um arquivo <c>.log</c> por execução do
/// aplicativo, escrito no encerramento, descrevendo a máquina, os recursos computacionais
/// disponíveis, os parâmetros em vigor, o que aconteceu durante a sessão (incluindo erros) e as
/// estatísticas agregadas da detecção.
/// </summary>
/// <remarks>
/// É relatório, não trilha de auditoria: as rodadas de detecção são AGREGADAS em estatísticas,
/// não gravadas uma a uma. Uma sessão de meia hora passa de 15 mil rodadas — uma linha por
/// rodada produzia um arquivo que ninguém lê e escondia justamente o que interessa (medianas,
/// p95, latência de aquisição, onde o tempo é gasto). Erros e eventos, por serem raros e
/// individualmente relevantes, continuam listados um a um.
/// </remarks>
public interface IPerformanceMonitor : IDisposable
{
    /// <summary>Caminho do arquivo desta execução, ou <c>null</c> se não foi possível prepará-lo.</summary>
    string? CaminhoArquivo { get; }

    /// <summary>Contabiliza uma rodada de detecção. Nunca lança — diagnóstico não pode derrubar a detecção.</summary>
    void RegistrarRodada(RegistroRodada registro);

    /// <summary>Anota um acontecimento relevante da sessão (troca de perfil, de modo, câmera iniciada).</summary>
    void RegistrarEvento(string descricao);

    /// <summary>
    /// Anota uma falha ocorrida durante a sessão. Separado de <see cref="RegistrarEvento"/>
    /// porque erros ganham seção própria no relatório — é a primeira coisa que se procura ao
    /// investigar uma execução problemática.
    /// </summary>
    void RegistrarErro(string contexto, string mensagem);
}
