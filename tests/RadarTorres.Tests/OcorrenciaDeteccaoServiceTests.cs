using System.Collections.Concurrent;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Controle de duplicidade, rastreamento e classificação por quadrante (critérios de aceitação
/// 6 a 11). Tudo em memória: o que está sob teste é a REGRA, não o SQLite — o dublê de
/// repositório aplica a mesma unicidade (rastreamento + quadrante) do banco.
/// </summary>
public sealed class OcorrenciaDeteccaoServiceTests
{
    private const int LarguraQuadro = 640;

    private static readonly CameraDevice CameraQ1 = new("cam-1", "Webcam frontal");
    private static readonly CameraDevice CameraQ2 = new("cam-2", "Webcam lateral");
    private static readonly CameraDevice CameraQ3 = new("cam-3", "Webcam traseira");
    private static readonly CameraDevice CameraQ4 = new("cam-4", "Webcam alta");

    private readonly RepositorioOcorrenciasEmMemoria _repositorio = new();
    private readonly CameraQuadranteFalso _quadrantes = new();
    private readonly LoggerFalso _logger = new();
    private readonly RastreamentoVisualService _rastreamento;
    private readonly OcorrenciaDeteccaoService _servico;

    private readonly PerfilAlvo _perfil = new()
    {
        Id = 1,
        Nome = "Bola vermelha",
        Cor = CorAlvo.Vermelho,
        Formato = FormatoAlvo.Redondo,
        TamanhoCm = 8
    };

    public OcorrenciaDeteccaoServiceTests()
    {
        _rastreamento = new RastreamentoVisualService(_logger);
        _servico = new OcorrenciaDeteccaoService(_rastreamento, _quadrantes, _repositorio, _logger);

        _quadrantes.Definir(CameraQ1.Id, Quadrant.Q1);
        _quadrantes.Definir(CameraQ2.Id, Quadrant.Q2);
        _quadrantes.Definir(CameraQ3.Id, Quadrant.Q3);
        _quadrantes.Definir(CameraQ4.Id, Quadrant.Q4);
    }

    private ContextoDeteccao Deteccao(CameraDevice camera, int x = 100, int y = 100, int largura = 80, int altura = 80) =>
        new(
            camera,
            _perfil,
            new AlvoVisualResultado(_perfil, new VisionDetection(x, y, largura, altura, 0.9), 3.5),
            Pontuacao: 0.7,
            LarguraQuadro: LarguraQuadro,
            Modo: SystemMode.AcompanharAlvos);

    /// <summary>Config com uma única confirmação, para o teste tratar da duplicidade e não da contagem.</summary>
    private static ConfiguracaoDeTeste ConfirmacaoImediata(int toleranciaSegundos = 10) =>
        new(v =>
        {
            v.ConfirmacoesParaRegistrarOcorrencia = 1;
            v.ToleranciaPerdaRastreamentoSegundos = toleranciaSegundos;
            v.IntervaloAtualizacaoAcompanhamentoSegundos = 1;
        });

    // ---------------------------------------------------------------- 6: um registro por acompanhamento

    [Fact]
    public void Alvo_acompanhado_continuamente_nao_gera_um_registro_por_quadro()
    {
        using var _ = ConfirmacaoImediata();

        // 30 rodadas do mesmo objeto, com a caixa variando levemente como num vídeo real.
        for (int rodada = 0; rodada < 30; rodada++)
        {
            _servico.RegistrarDeteccao(Deteccao(CameraQ1, x: 100 + (rodada % 4), y: 100 + (rodada % 3)));
        }

        Assert.Single(_repositorio.GetAll());
    }

    [Fact]
    public void Primeira_rodada_nao_abre_ocorrencia_quando_sao_exigidas_duas_confirmacoes()
    {
        using var _ = new ConfiguracaoDeTeste(v => v.ConfirmacoesParaRegistrarOcorrencia = 2);

        ResultadoRegistroOcorrencia primeira = _servico.RegistrarDeteccao(Deteccao(CameraQ1));

        Assert.True(primeira.AguardandoConfirmacao);
        Assert.Empty(_repositorio.GetAll());

        ResultadoRegistroOcorrencia segunda = _servico.RegistrarDeteccao(Deteccao(CameraQ1));

        Assert.True(segunda.Criada);
        Assert.Single(_repositorio.GetAll());
    }

    [Fact]
    public void Acompanhamento_em_curso_avanca_o_visto_por_ultimo_em_vez_de_duplicar()
    {
        using var _ = ConfirmacaoImediata();

        ResultadoRegistroOcorrencia criacao = _servico.RegistrarDeteccao(Deteccao(CameraQ1));
        Assert.True(criacao.Criada);

        Thread.Sleep(1100); // passa o intervalo mínimo entre gravações de acompanhamento
        ResultadoRegistroOcorrencia seguinte = _servico.RegistrarDeteccao(Deteccao(CameraQ1));

        Assert.False(seguinte.Criada);
        Assert.Single(_repositorio.GetAll());
        Assert.NotNull(_repositorio.GetAll()[0].DataHoraUltimaDeteccao);
    }

    [Fact]
    public void Objeto_em_posicao_completamente_diferente_abre_outra_ocorrencia()
    {
        using var _ = ConfirmacaoImediata();

        _servico.RegistrarDeteccao(Deteccao(CameraQ1, x: 20, y: 20, largura: 60, altura: 60));
        _servico.RegistrarDeteccao(Deteccao(CameraQ1, x: 480, y: 360, largura: 60, altura: 60));

        // Dois objetos distintos no mesmo quadrante não são o mesmo acompanhamento.
        Assert.Equal(2, _repositorio.GetAll().Count);
    }

    // ---------------------------------------------------------------- 7: perda temporária ≠ abate

    [Fact]
    public void Perda_temporaria_de_deteccao_nao_marca_abatido_nem_duplica_ao_voltar()
    {
        using var _ = ConfirmacaoImediata(toleranciaSegundos: 30);

        ResultadoRegistroOcorrencia inicial = _servico.RegistrarDeteccao(Deteccao(CameraQ1));
        int ocorrenciaId = inicial.Ocorrencia!.Id;

        // Várias rodadas sem nenhum alvo (oclusão/objeto fora do quadro).
        for (int i = 0; i < 20; i++) _servico.RegistrarRodadaSemDeteccao(DateTime.Now);

        Assert.Equal(EstadoOcorrencia.Detectado, _repositorio.GetById(ocorrenciaId)!.Estado);

        // O objeto volta: mesma identidade, nenhuma ocorrência nova.
        ResultadoRegistroOcorrencia retorno = _servico.RegistrarDeteccao(Deteccao(CameraQ1));

        Assert.False(retorno.Criada);
        Assert.Single(_repositorio.GetAll());
        Assert.Equal(EstadoOcorrencia.Detectado, _repositorio.GetById(ocorrenciaId)!.Estado);
    }

    [Fact]
    public void Abate_so_acontece_por_evento_explicito()
    {
        using var _ = ConfirmacaoImediata(toleranciaSegundos: 1);

        int ocorrenciaId = _servico.RegistrarDeteccao(Deteccao(CameraQ1)).Ocorrencia!.Id;

        // Estoura a tolerância: o acompanhamento é encerrado...
        Thread.Sleep(1200);
        _servico.RegistrarRodadaSemDeteccao(DateTime.Now);

        // ...mas a ocorrência continua apenas "detectada" — encerrar identidade não é abater.
        Assert.Equal(EstadoOcorrencia.Detectado, _repositorio.GetById(ocorrenciaId)!.Estado);

        // Só o evento explícito (acionamento) muda o estado, e ainda assim para PENDENTE.
        Assert.True(_servico.MarcarAbatePendente(ocorrenciaId, "operador", "acionamento executado"));
        Assert.Equal(EstadoOcorrencia.AbatePendente, _repositorio.GetById(ocorrenciaId)!.Estado);
    }

    [Fact]
    public void Acompanhamento_expirado_faz_o_retorno_abrir_nova_ocorrencia()
    {
        using var _ = ConfirmacaoImediata(toleranciaSegundos: 1);

        _servico.RegistrarDeteccao(Deteccao(CameraQ1));

        Thread.Sleep(1200);
        _servico.RegistrarRodadaSemDeteccao(DateTime.Now);

        _servico.RegistrarDeteccao(Deteccao(CameraQ1));

        Assert.Equal(2, _repositorio.GetAll().Count);
    }

    // ---------------------------------------------------------------- 8, 9 e 10: quadrantes

    [Fact]
    public void Mesmo_alvo_gera_uma_ocorrencia_por_quadrante()
    {
        using var _ = ConfirmacaoImediata();

        foreach (CameraDevice camera in new[] { CameraQ1, CameraQ2, CameraQ3, CameraQ4 })
        {
            _servico.RegistrarDeteccao(Deteccao(camera));
        }

        List<ObjetoDetectado> ocorrencias = _repositorio.GetAll().ToList();

        Assert.Equal(4, ocorrencias.Count);
        Assert.Equal(new[] { "Q1", "Q2", "Q3", "Q4" }, ocorrencias.Select(o => o.Quadrante).OrderBy(q => q).ToArray());

        // Um ÚNICO alvo cadastrado por trás das quatro ocorrências.
        Assert.All(ocorrencias, o => Assert.Equal(_perfil.Id, o.PerfilAlvoId));
        Assert.Single(ocorrencias.Select(o => o.PerfilAlvoId).Distinct());
    }

    [Fact]
    public void Ocorrencias_repetidas_no_mesmo_quadrante_sao_controladas_pelo_rastreamento()
    {
        using var _ = ConfirmacaoImediata();

        for (int i = 0; i < 10; i++) _servico.RegistrarDeteccao(Deteccao(CameraQ1));
        for (int i = 0; i < 10; i++) _servico.RegistrarDeteccao(Deteccao(CameraQ2));

        Assert.Equal(2, _repositorio.GetAll().Count);
    }

    [Fact]
    public void Cada_ocorrencia_aponta_a_camera_e_o_quadrante_responsaveis()
    {
        using var _ = ConfirmacaoImediata();

        _servico.RegistrarDeteccao(Deteccao(CameraQ3));
        ObjetoDetectado ocorrencia = _repositorio.GetAll().Single();

        Assert.Equal("Q3", ocorrencia.Quadrante);
        Assert.Equal(CameraQ3.Id, ocorrencia.CameraDispositivoId);
        Assert.Equal(CameraQ3.Name, ocorrencia.CameraNome);
        Assert.Equal("Câmera", ocorrencia.Dispositivo);
        Assert.False(string.IsNullOrEmpty(ocorrencia.RastreamentoId));
    }

    [Fact]
    public void Camera_sem_quadrante_atribuido_cai_no_calculo_geometrico()
    {
        using var _ = ConfirmacaoImediata();

        var semQuadrante = new CameraDevice("cam-sem", "Webcam sem quadrante");

        // Caixa em x=100..180 num quadro de 640: centro em 140, à ESQUERDA do centro do quadro
        // (320) → ângulo negativo → X negativo e Y positivo → Q2, pela convenção de
        // QuadrantHelper. É o comportamento anterior ao cadastro de quadrantes, preservado.
        ResultadoRegistroOcorrencia esquerda = _servico.RegistrarDeteccao(Deteccao(semQuadrante));

        Assert.Equal(Quadrant.Q2, esquerda.Quadrante);
        Assert.Equal("Q2", _repositorio.GetAll().Single().Quadrante);

        // E um objeto à direita do centro cai em Q1 — confirma que é a posição que decide.
        var outraCamera = new CameraDevice("cam-sem-2", "Outra sem quadrante");
        ResultadoRegistroOcorrencia direita = _servico.RegistrarDeteccao(Deteccao(outraCamera, x: 500, y: 100));

        Assert.Equal(Quadrant.Q1, direita.Quadrante);
    }

    // ---------------------------------------------------------------- 11: concorrência

    [Fact]
    public void Cameras_processando_em_paralelo_nao_duplicam_ocorrencias()
    {
        using var _ = ConfirmacaoImediata();

        CameraDevice[] cameras = { CameraQ1, CameraQ2, CameraQ3, CameraQ4 };
        var erros = new ConcurrentBag<Exception>();

        // 4 câmeras × 25 rodadas simultâneas, como no uso real (uma thread por câmera).
        Parallel.ForEach(cameras, camera =>
        {
            try
            {
                for (int rodada = 0; rodada < 25; rodada++)
                {
                    _servico.RegistrarDeteccao(Deteccao(camera, x: 100 + (rodada % 3), y: 100 + (rodada % 2)));
                }
            }
            catch (Exception ex)
            {
                erros.Add(ex);
            }
        });

        Assert.Empty(erros);

        List<ObjetoDetectado> ocorrencias = _repositorio.GetAll().ToList();
        Assert.Equal(4, ocorrencias.Count);
        Assert.Equal(4, ocorrencias.Select(o => o.Quadrante).Distinct().Count());
        Assert.Equal(4, ocorrencias.Select(o => o.RastreamentoId).Distinct().Count());
    }

    [Fact]
    public void Parada_da_camera_encerra_apenas_os_acompanhamentos_dela()
    {
        using var _ = ConfirmacaoImediata();

        _servico.RegistrarDeteccao(Deteccao(CameraQ1));
        _servico.RegistrarDeteccao(Deteccao(CameraQ2));
        Assert.Equal(2, _rastreamento.Ativos.Count);

        _servico.EncerrarCamera(CameraQ1.Id, "teste");

        Assert.Single(_rastreamento.Ativos);
        Assert.Equal(CameraQ2.Id, _rastreamento.Ativos[0].CameraDispositivoId);

        // A ocorrência da câmera parada continua no histórico, apenas detectada.
        Assert.Equal(2, _repositorio.GetAll().Count);
        Assert.All(_repositorio.GetAll(), o => Assert.Equal(EstadoOcorrencia.Detectado, o.Estado));
    }

    // ---------------------------------------------------------------- Ciclo de abate

    [Fact]
    public void Abate_confirmado_encerra_o_acompanhamento_e_libera_nova_ocorrencia()
    {
        using var _ = ConfirmacaoImediata();

        int ocorrenciaId = _servico.RegistrarDeteccao(Deteccao(CameraQ1)).Ocorrencia!.Id;

        _servico.MarcarAbatePendente(ocorrenciaId, "operador", "acionamento");
        _repositorio.AtualizarEstado(ocorrenciaId, EstadoOcorrencia.Abatido, "operador", "confirmado");

        Thread.Sleep(1100);
        _servico.RegistrarDeteccao(Deteccao(CameraQ1));   // encerra o acompanhamento abatido
        _servico.RegistrarDeteccao(Deteccao(CameraQ1));   // e abre uma ocorrência nova

        Assert.Equal(2, _repositorio.GetAll().Count);
        Assert.Equal(EstadoOcorrencia.Abatido, _repositorio.GetById(ocorrenciaId)!.Estado);
    }

    [Fact]
    public void Historico_de_estados_registra_cada_transicao()
    {
        using var _ = ConfirmacaoImediata();

        int ocorrenciaId = _servico.RegistrarDeteccao(Deteccao(CameraQ1)).Ocorrencia!.Id;

        _servico.MarcarAbatePendente(ocorrenciaId, "operador", "acionamento executado");
        _repositorio.AtualizarEstado(ocorrenciaId, EstadoOcorrencia.Abatido, "operador", "abate confirmado");

        IReadOnlyList<OcorrenciaEstadoTransicao> historico = _repositorio.ObterHistoricoEstados(ocorrenciaId);

        Assert.Equal(
            new[] { EstadoOcorrencia.Detectado, EstadoOcorrencia.AbatePendente, EstadoOcorrencia.Abatido },
            historico.Select(h => h.Estado).ToArray());
    }
}
