using System;
using System.Drawing;
using RadarTorres.App.Configuration;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IOcorrenciaDeteccaoService"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Quadrante.</b> Vem da câmera que detectou (<see cref="ICameraQuadranteService"/>), não da
/// posição estimada do objeto: é a câmera que tem responsabilidade espacial atribuída pelo
/// operador. Quando a câmera não tem quadrante atribuído, cai no cálculo geométrico pela posição
/// — que era o comportamento anterior, preservado para não exigir configuração antes de o
/// recurso voltar a funcionar.
/// </para>
/// <para>
/// <b>Por que a ocorrência só abre depois de algumas confirmações.</b> A pontuação do
/// reconhecimento oscila entre quadros; registrar na primeira rodada encheria o histórico de
/// ocorrências que não se repetem. Ver
/// <see cref="VisionSettings.ConfirmacoesParaRegistrarOcorrencia"/>.
/// </para>
/// <para>
/// <b>Abate.</b> Nada aqui conclui abate. Desaparecer da imagem encerra o acompanhamento e nada
/// mais; a ocorrência só muda de estado por acionamento bem-sucedido
/// (<see cref="MarcarAbatePendente"/>) e, depois, por confirmação explícita de um operador na
/// tela "Objetos Detectados".
/// </para>
/// </remarks>
public sealed class OcorrenciaDeteccaoService : IOcorrenciaDeteccaoService
{
    private readonly IRastreamentoVisualService _rastreamento;
    private readonly ICameraQuadranteService _cameraQuadranteService;
    private readonly IObjetoDetectadoRepository _repository;
    private readonly ILoggingService _logger;

    public OcorrenciaDeteccaoService(
        IRastreamentoVisualService rastreamento,
        ICameraQuadranteService cameraQuadranteService,
        IObjetoDetectadoRepository repository,
        ILoggingService logger)
    {
        _rastreamento = rastreamento;
        _cameraQuadranteService = cameraQuadranteService;
        _repository = repository;
        _logger = logger;
    }

    public ResultadoRegistroOcorrencia RegistrarDeteccao(ContextoDeteccao contexto)
    {
        DateTime agora = DateTime.Now;
        VisionSettings config = AppConfig.Current.VisionSettings;

        _rastreamento.EncerrarInativos(agora);

        VisionDetection deteccao = contexto.Resultado.Deteccao;
        var caixa = new Rectangle(deteccao.BoxXPx, deteccao.BoxYPx, deteccao.BoxWidthPx, deteccao.BoxHeightPx);

        (double x, double y, double anguloGraus) = Posicionar(contexto);
        Quadrant quadrante = ResolverQuadrante(contexto.Camera, x, y);

        RastreioVisual rastreio = _rastreamento.ObterOuCriar(
            contexto.Camera.Id, quadrante, contexto.Perfil.Id, caixa, agora);

        if (rastreio.Confirmacoes < Math.Max(1, config.ConfirmacoesParaRegistrarOcorrencia))
        {
            return new ResultadoRegistroOcorrencia(null, Criada: false, quadrante, AguardandoConfirmacao: true);
        }

        // --- Acompanhamento já tem ocorrência aberta: nada de linha nova.
        if (rastreio.ObjetoDetectadoId is { } ocorrenciaId)
        {
            return AtualizarAcompanhamento(rastreio, ocorrenciaId, quadrante, agora, config);
        }

        // --- Primeira vez que este acompanhamento chega ao mínimo de confirmações: abre a ocorrência.
        try
        {
            var nova = new ObjetoDetectado
            {
                Tipo = contexto.Perfil.Nome,
                X = x,
                Y = y,
                Z = null,
                Quadrante = QuadrantHelper.ToDisplayLabel(quadrante),
                DataHora = rastreio.PrimeiraDeteccao,
                Dispositivo = "Câmera",
                NivelConfianca = contexto.Pontuacao,
                Observacao =
                    $"Detecção por visão computacional — perfil \"{contexto.Perfil.Nome}\" " +
                    $"({contexto.Perfil.Cor}/{contexto.Perfil.Formato}, {contexto.Perfil.DimensoesDescricao}), " +
                    $"{anguloGraus:0.0}° do centro" +
                    (contexto.Modo is { } modo ? $", modo {modo}." : $", painel da câmera \"{contexto.Camera.Name}\"."),
                ReferenciaImagem = contexto.Perfil.CaminhoFotoReferencia,
                PerfilAlvoId = contexto.Perfil.Id,
                CameraDispositivoId = contexto.Camera.Id,
                CameraNome = contexto.Camera.Name,
                RastreamentoId = rastreio.Id,
                Estado = EstadoOcorrencia.Detectado,
                DataHoraUltimaDeteccao = null
            };

            ObjetoDetectado gravada = _repository.AdicionarSeNaoExistir(nova);
            rastreio.ObjetoDetectadoId = gravada.Id;
            rastreio.UltimaAtualizacaoPersistida = agora;

            _logger.Success(
                $"Ocorrência #{gravada.Id} aberta: alvo \"{contexto.Perfil.Nome}\" em " +
                $"{QuadrantHelper.ToDisplayLabel(quadrante)} pela câmera \"{contexto.Camera.Name}\" " +
                $"a {contexto.Resultado.DistanciaMetros:0.0} m.");

            return new ResultadoRegistroOcorrencia(gravada, Criada: true, quadrante, AguardandoConfirmacao: false);
        }
        catch (Exception ex)
        {
            // Falhar ao gravar o histórico não pode interromper a detecção nem o alerta — o
            // alerta ao operador vale mais que a linha no banco.
            _logger.Error($"Alvo detectado, mas não foi possível abrir a ocorrência no histórico: {ex.Message}");
            return new ResultadoRegistroOcorrencia(null, Criada: false, quadrante, AguardandoConfirmacao: false);
        }
    }

    public void RegistrarRodadaSemDeteccao(DateTime agora) => _rastreamento.EncerrarInativos(agora);

    public void EncerrarCamera(string cameraDispositivoId, string motivo) =>
        _rastreamento.EncerrarDaCamera(cameraDispositivoId, motivo);

    public bool MarcarAbatePendente(int objetoDetectadoId, string? usuario, string? observacao)
    {
        try
        {
            return _repository.AtualizarEstado(objetoDetectadoId, EstadoOcorrencia.AbatePendente, usuario, observacao);
        }
        catch (Exception ex)
        {
            _logger.Error($"Não foi possível marcar a ocorrência #{objetoDetectadoId} como pendente de confirmação de abate: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Mesmo objeto, mesma ocorrência: só avança o "visto por último" (com folga entre
    /// gravações) e reage se a ocorrência já foi abatida enquanto era acompanhada.
    /// </summary>
    private ResultadoRegistroOcorrencia AtualizarAcompanhamento(
        RastreioVisual rastreio, int ocorrenciaId, Quadrant quadrante, DateTime agora, VisionSettings config)
    {
        ObjetoDetectado? ocorrencia = null;

        bool devePersistir = (agora - rastreio.UltimaAtualizacaoPersistida).TotalSeconds
            >= Math.Max(1, config.IntervaloAtualizacaoAcompanhamentoSegundos);

        if (devePersistir)
        {
            _repository.RegistrarDeteccaoContinuada(ocorrenciaId, agora);
            rastreio.UltimaAtualizacaoPersistida = agora;

            ocorrencia = _repository.GetById(ocorrenciaId);

            // Abate confirmado fecha o ciclo de vida desta ocorrência: o acompanhamento é
            // encerrado para que uma detecção posterior abra uma ocorrência NOVA, em vez de
            // continuar escrevendo numa que já foi concluída.
            if (ocorrencia?.Estado == EstadoOcorrencia.Abatido)
            {
                _rastreamento.Encerrar(rastreio, $"ocorrência #{ocorrenciaId} com abate confirmado");
            }
        }

        return new ResultadoRegistroOcorrencia(ocorrencia, Criada: false, quadrante, AguardandoConfirmacao: false);
    }

    /// <summary>
    /// A câmera só informa direção horizontal: o ângulo sai da posição do centro da caixa dentro
    /// do campo de visão, e a distância vira X/Y no mesmo plano usado pelos alvos do radar
    /// (Y para frente da câmera), para as duas origens conviverem na mesma tela.
    /// </summary>
    private static (double X, double Y, double AnguloGraus) Posicionar(ContextoDeteccao contexto)
    {
        VisionDetection deteccao = contexto.Resultado.Deteccao;

        double centroXPx = deteccao.BoxXPx + (deteccao.BoxWidthPx / 2.0);
        double anguloGraus = ((centroXPx / Math.Max(1, contexto.LarguraQuadro)) - 0.5)
            * AppConfig.Current.VisionSettings.CameraFovHorizontalGraus;
        double anguloRad = anguloGraus * Math.PI / 180.0;

        return (
            contexto.Resultado.DistanciaMetros * Math.Sin(anguloRad),
            contexto.Resultado.DistanciaMetros * Math.Cos(anguloRad),
            anguloGraus);
    }

    private Quadrant ResolverQuadrante(CameraDevice camera, double x, double y)
    {
        Quadrant atribuido = _cameraQuadranteService.ObterQuadrante(camera.Id);
        return atribuido != Quadrant.None ? atribuido : QuadrantHelper.Determine(x, y);
    }
}
