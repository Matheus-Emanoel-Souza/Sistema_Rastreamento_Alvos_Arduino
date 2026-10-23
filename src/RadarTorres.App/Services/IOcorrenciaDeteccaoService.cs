using System;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Tudo o que uma rodada de detecção de uma câmera precisa informar para virar (ou não) uma ocorrência.</summary>
/// <param name="Camera">Câmera que fez a detecção.</param>
/// <param name="Perfil">Perfil de alvo reconhecido.</param>
/// <param name="Resultado">Caixa e distância estimada do alvo aceito.</param>
/// <param name="Pontuacao">Melhor pontuação da rodada (similaridade com as fotos, ou fração de cor).</param>
/// <param name="LarguraQuadro">Largura do quadro em pixels — converte a posição da caixa em ângulo.</param>
/// <param name="Modo">Modo de operação no instante da detecção, quando quem detectou o conhece (card de Monitoramento). <c>null</c> nos painéis da tela "Câmeras", que não acompanham o modo — melhor omitir do que registrar um valor que não foi verificado.</param>
public sealed record ContextoDeteccao(
    CameraDevice Camera,
    PerfilAlvo Perfil,
    AlvoVisualResultado Resultado,
    double Pontuacao,
    int LarguraQuadro,
    SystemMode? Modo);

/// <summary>O que o registro de uma detecção produziu.</summary>
/// <param name="Ocorrencia">A ocorrência correspondente (nova ou a que já estava aberta), ou <c>null</c> se ainda não foi aberta.</param>
/// <param name="Criada"><c>true</c> apenas na rodada em que a ocorrência foi aberta no histórico.</param>
/// <param name="Quadrante">Quadrante atribuído à ocorrência.</param>
/// <param name="AguardandoConfirmacao"><c>true</c> enquanto o objeto não acumulou confirmações suficientes para abrir ocorrência.</param>
public sealed record ResultadoRegistroOcorrencia(
    ObjetoDetectado? Ocorrencia,
    bool Criada,
    Quadrant Quadrante,
    bool AguardandoConfirmacao);

/// <summary>
/// Converte detecções por câmera em ocorrências no histórico, aplicando a regra central do
/// sistema: <b>enquanto o mesmo objeto continuar sendo acompanhado no mesmo quadrante e não for
/// abatido, não se cria ocorrência nova</b> — o que avança é apenas o "visto por último". Um
/// mesmo alvo cadastrado pode, assim, originar até quatro ocorrências (uma por quadrante) sem
/// nunca duplicar dentro do mesmo quadrante.
/// </summary>
public interface IOcorrenciaDeteccaoService
{
    /// <summary>Registra (ou reaproveita) a ocorrência correspondente a esta detecção.</summary>
    ResultadoRegistroOcorrencia RegistrarDeteccao(ContextoDeteccao contexto);

    /// <summary>
    /// Informa que esta rodada da câmera não reconheceu nada. Serve para aplicar a tolerância de
    /// perda — <b>não</b> encerra nem abate nada por si só.
    /// </summary>
    void RegistrarRodadaSemDeteccao(DateTime agora);

    /// <summary>A câmera parou (troca de dispositivo, erro, saída da tela): fecha os acompanhamentos dela.</summary>
    void EncerrarCamera(string cameraDispositivoId, string motivo);

    /// <summary>
    /// Marca a ocorrência ligada a um acionamento bem-sucedido como pendente de confirmação de
    /// abate. Chamada por <see cref="IFireControlService"/>.
    /// </summary>
    bool MarcarAbatePendente(int objetoDetectadoId, string? usuario, string? observacao);

    /// <summary>
    /// Disparado a cada rodada de <see cref="RegistrarDeteccao"/> com a pontuação de
    /// identificação (0 a 1) da câmera para o quadrante detectado — é a "certeza de que é um
    /// alvo" que <see cref="Target.Confidence"/> reflete, usada pela regra de disparo por
    /// certeza mínima (ver <see cref="IFireControlService"/>).
    /// </summary>
    event EventHandler<(Quadrant Quadrante, double Confianca)>? ConfiancaAtualizada;
}
