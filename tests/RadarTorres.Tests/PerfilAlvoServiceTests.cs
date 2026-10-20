using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cadastro e edição de alvos (critérios de aceitação 1 a 5): mínimo de fotografias, edição sem
/// criar registro novo, preservação do vínculo com o histórico e dimensões separadas de objetos
/// retangulares.
/// </summary>
public sealed class PerfilAlvoServiceTests : IDisposable
{
    private readonly string _pasta = FotosDeTeste.CriarPasta();
    private readonly RepositorioPerfisEmMemoria _repositorio = new();
    private readonly LoggerFalso _logger = new();
    private readonly PerfilAlvoService _servico;

    public PerfilAlvoServiceTests()
    {
        _servico = new PerfilAlvoService(_repositorio, _logger);
    }

    public void Dispose() => FotosDeTeste.Limpar(_pasta);

    private List<FotoReferenciaAlvo> Fotos(int quantidade) => Enumerable.Range(0, quantidade)
        .Select(_ => new FotoReferenciaAlvo { Caminho = FotosDeTeste.FotoValida(_pasta), CapturadaEm = DateTime.Now })
        .ToList();

    // ---------------------------------------------------------------- 1 e 2: mínimo de fotos

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Add_recusa_cadastro_com_menos_de_tres_fotografias(int quantidade)
    {
        ResultadoPerfilAlvo resultado = _servico.Add(
            "Bola vermelha", CorAlvo.Vermelho, FormatoAlvo.Redondo, DimensoesAlvo.Unica(8), Fotos(quantidade));

        Assert.False(resultado.Sucesso);
        Assert.Contains("mínimo 3", resultado.Erro!.Replace("no mínimo 3", "mínimo 3"));
        Assert.Empty(_servico.Perfis);
    }

    [Fact]
    public void Add_aceita_cadastro_com_tres_fotografias_validas()
    {
        ResultadoPerfilAlvo resultado = _servico.Add(
            "Bola vermelha", CorAlvo.Vermelho, FormatoAlvo.Redondo, DimensoesAlvo.Unica(8), Fotos(3));

        Assert.True(resultado.Sucesso, resultado.Erro);
        Assert.Equal(3, resultado.Perfil!.TotalFotos);
        Assert.Single(_servico.Perfis);
    }

    [Fact]
    public void Add_recusa_quando_uma_das_fotografias_e_invalida()
    {
        List<FotoReferenciaAlvo> fotos = Fotos(2);
        fotos.Add(new FotoReferenciaAlvo { Caminho = FotosDeTeste.ArquivoCorrompido(_pasta) });

        ResultadoPerfilAlvo resultado = _servico.Add(
            "Caderno", CorAlvo.Verde, FormatoAlvo.Retangular, DimensoesAlvo.Retangulo(21, 29.7), fotos);

        Assert.False(resultado.Sucesso);
        Assert.Contains("inválida", resultado.Erro!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(_servico.Perfis);
    }

    [Fact]
    public void Add_nao_conta_a_mesma_fotografia_duas_vezes()
    {
        string repetida = FotosDeTeste.FotoValida(_pasta);
        var fotos = new List<FotoReferenciaAlvo>
        {
            new() { Caminho = repetida },
            new() { Caminho = repetida },
            new() { Caminho = repetida }
        };

        ResultadoPerfilAlvo resultado = _servico.Add(
            "Repetida", CorAlvo.Azul, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(5), fotos);

        Assert.False(resultado.Sucesso);
    }

    // ---------------------------------------------------------------- 3 e 4: edição

    [Fact]
    public void Atualizar_altera_o_mesmo_registro_sem_criar_outro()
    {
        PerfilAlvo original = _servico.Add(
            "Bola", CorAlvo.Vermelho, FormatoAlvo.Redondo, DimensoesAlvo.Unica(8), Fotos(3)).Perfil!;
        int idOriginal = original.Id;

        ResultadoPerfilAlvo resultado = _servico.Atualizar(
            original, "Bola grande", CorAlvo.Azul, FormatoAlvo.Redondo, DimensoesAlvo.Unica(12), original.Fotos.ToList());

        Assert.True(resultado.Sucesso, resultado.Erro);
        Assert.Single(_servico.Perfis);
        Assert.Equal(idOriginal, resultado.Perfil!.Id);
        Assert.Same(original, resultado.Perfil);
        Assert.Equal("Bola grande", original.Nome);
        Assert.Equal(CorAlvo.Azul, original.Cor);
        Assert.Equal(12, original.TamanhoCm);
    }

    [Fact]
    public void Atualizar_preserva_o_vinculo_do_historico_de_deteccoes()
    {
        PerfilAlvo perfil = _servico.Add(
            "Alvo", CorAlvo.Verde, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(10), Fotos(3)).Perfil!;

        // Uma ocorrência já registrada aponta para o alvo pelo Id (ver ObjetoDetectado.PerfilAlvoId).
        var ocorrencia = new ObjetoDetectado { PerfilAlvoId = perfil.Id, Tipo = perfil.Nome, Quadrante = "Q1" };

        _servico.Atualizar(perfil, "Alvo renomeado", CorAlvo.Azul, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(11), perfil.Fotos.ToList());

        Assert.Equal(perfil.Id, ocorrencia.PerfilAlvoId);
        Assert.NotNull(_servico.Perfis.FirstOrDefault(p => p.Id == ocorrencia.PerfilAlvoId));
    }

    [Fact]
    public void Atualizar_permite_adicionar_e_remover_fotografias()
    {
        PerfilAlvo perfil = _servico.Add(
            "Alvo", CorAlvo.Verde, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(10), Fotos(3)).Perfil!;

        List<FotoReferenciaAlvo> novas = perfil.Fotos.Skip(1).ToList();     // remove a primeira
        novas.AddRange(Fotos(2));                                            // e acrescenta duas

        ResultadoPerfilAlvo resultado = _servico.Atualizar(
            perfil, perfil.Nome, perfil.Cor, perfil.Formato, DimensoesAlvo.Unica(perfil.TamanhoCm), novas);

        Assert.True(resultado.Sucesso, resultado.Erro);
        Assert.Equal(4, perfil.TotalFotos);
    }

    [Fact]
    public void Atualizar_recusa_perfil_que_nao_esta_cadastrado()
    {
        var estranho = new PerfilAlvo { Id = 999, Nome = "Fora da lista" };

        ResultadoPerfilAlvo resultado = _servico.Atualizar(
            estranho, "X", CorAlvo.Azul, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(5), Fotos(3));

        Assert.False(resultado.Sucesso);
        Assert.Empty(_servico.Perfis);
    }

    [Fact]
    public void Atualizar_nao_exige_o_minimo_de_fotografias()
    {
        PerfilAlvo perfil = _servico.Add(
            "Alvo", CorAlvo.Verde, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(10), Fotos(3)).Perfil!;

        ResultadoPerfilAlvo resultado = _servico.Atualizar(
            perfil, perfil.Nome, perfil.Cor, perfil.Formato, DimensoesAlvo.Unica(10), perfil.Fotos.Take(1).ToList());

        Assert.True(resultado.Sucesso, resultado.Erro);
        Assert.Equal(1, perfil.TotalFotos);
    }

    // ---------------------------------------------------------------- 5: dimensões

    [Fact]
    public void Objeto_retangular_guarda_largura_e_altura_independentes()
    {
        ResultadoPerfilAlvo resultado = _servico.Add(
            "Caderno", CorAlvo.Verde, FormatoAlvo.Retangular, DimensoesAlvo.Retangulo(21, 29.7), Fotos(3));

        PerfilAlvo perfil = resultado.Perfil!;

        Assert.True(resultado.Sucesso, resultado.Erro);
        Assert.Equal(21, perfil.LarguraCm);
        Assert.Equal(29.7, perfil.AlturaCm);

        // TamanhoCm continua preenchido com a MAIOR dimensão: é o campo que a estimativa de
        // distância usa e o que mantém compatibilidade com os perfis antigos.
        Assert.Equal(29.7, perfil.TamanhoCm);
        Assert.Equal(29.7, perfil.MaiorDimensaoCm);
        Assert.Contains("21", perfil.DimensoesDescricao);
        Assert.Contains("29", perfil.DimensoesDescricao);
    }

    [Theory]
    [InlineData(null, 10.0)]
    [InlineData(10.0, null)]
    [InlineData(0.0, 10.0)]
    [InlineData(10.0, -3.0)]
    public void Objeto_retangular_recusa_dimensao_nula_ou_negativa(double? largura, double? altura)
    {
        ResultadoPerfilAlvo resultado = _servico.Add(
            "Caderno", CorAlvo.Verde, FormatoAlvo.Retangular, new DimensoesAlvo(null, largura, altura), Fotos(3));

        Assert.False(resultado.Sucesso);
        Assert.Contains("largura e altura", resultado.Erro!, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(FormatoAlvo.Quadrado)]
    [InlineData(FormatoAlvo.Redondo)]
    public void Formatos_nao_retangulares_usam_dimensao_unica(FormatoAlvo formato)
    {
        PerfilAlvo perfil = _servico.Add("Alvo", CorAlvo.Azul, formato, DimensoesAlvo.Unica(7.5), Fotos(3)).Perfil!;

        Assert.Equal(7.5, perfil.TamanhoCm);
        Assert.Null(perfil.LarguraCm);
        Assert.Null(perfil.AlturaCm);
        Assert.Equal(7.5, perfil.MaiorDimensaoCm);
    }

    [Fact]
    public void Formato_nao_retangular_recusa_dimensao_invalida()
    {
        ResultadoPerfilAlvo resultado = _servico.Add(
            "Alvo", CorAlvo.Azul, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(0), Fotos(3));

        Assert.False(resultado.Sucesso);
    }

    /// <summary>
    /// Perfil retangular gravado antes de existirem largura/altura: a leitura não pode descartar
    /// o dado antigo nem passar a reportar zero.
    /// </summary>
    [Fact]
    public void Perfil_retangular_legado_mantem_a_dimensao_antiga_utilizavel()
    {
        var legado = new PerfilAlvo
        {
            Id = 7,
            Nome = "Caderno antigo",
            Formato = FormatoAlvo.Retangular,
            TamanhoCm = 29.7
        };

        Assert.Equal(29.7, legado.MaiorDimensaoCm);
        Assert.Contains("29", legado.DimensoesDescricao);
    }

    // ---------------------------------------------------------------- Marcação do objeto

    [Fact]
    public void Marcacao_do_objeto_por_foto_e_persistida_na_edicao()
    {
        PerfilAlvo perfil = _servico.Add(
            "Alvo", CorAlvo.Verde, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(10), Fotos(3)).Perfil!;

        // A tela edita uma CÓPIA das fotos e grava tudo em Atualizar (ver PerfilAlvoViewModel).
        List<FotoReferenciaAlvo> rascunho = perfil.ClonarFotos();
        rascunho[1].Recorte = new RecorteAlvo(10, 20, 30, 40);

        ResultadoPerfilAlvo resultado = _servico.Atualizar(
            perfil, perfil.Nome, perfil.Cor, perfil.Formato, DimensoesAlvo.Unica(10), rascunho);

        Assert.True(resultado.Sucesso, resultado.Erro);
        Assert.False(perfil.Fotos[0].TemRecorte);
        Assert.True(perfil.Fotos[1].TemRecorte);
        Assert.Equal(new RecorteAlvo(10, 20, 30, 40), perfil.Fotos[1].Recorte);
    }

    [Fact]
    public void Clonar_fotos_isola_o_rascunho_do_perfil_salvo()
    {
        PerfilAlvo perfil = _servico.Add(
            "Alvo", CorAlvo.Verde, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(10), Fotos(3)).Perfil!;

        List<FotoReferenciaAlvo> rascunho = perfil.ClonarFotos();
        rascunho[0].Recorte = new RecorteAlvo(1, 2, 3, 4);

        // Desistir da edição (não chamar Atualizar) não pode ter alterado o perfil salvo.
        Assert.Null(perfil.Fotos[0].Recorte);
    }

    [Fact]
    public void Recorte_sem_area_nao_vale_como_marcacao()
    {
        var foto = new FotoReferenciaAlvo { Caminho = FotosDeTeste.FotoValida(_pasta) };

        foto.Recorte = new RecorteAlvo(10, 10, 0, 50);
        Assert.False(foto.TemRecorte);

        foto.Recorte = new RecorteAlvo(10, 10, 40, 50);
        Assert.True(foto.TemRecorte);
    }

    // ---------------------------------------------------------------- 13: nada quebrou

    [Fact]
    public void SelecionarAtivo_mantem_apenas_um_perfil_ativo()
    {
        PerfilAlvo primeiro = _servico.Add("A", CorAlvo.Azul, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(5), Fotos(3)).Perfil!;
        PerfilAlvo segundo = _servico.Add("B", CorAlvo.Verde, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(5), Fotos(3)).Perfil!;

        _servico.SelecionarAtivo(primeiro);
        _servico.SelecionarAtivo(segundo);

        Assert.False(primeiro.Selecionado);
        Assert.True(segundo.Selecionado);
        Assert.Same(segundo, _servico.PerfilAtivo);
    }

    [Fact]
    public void Remove_apaga_somente_o_perfil_indicado()
    {
        PerfilAlvo primeiro = _servico.Add("A", CorAlvo.Azul, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(5), Fotos(3)).Perfil!;
        PerfilAlvo segundo = _servico.Add("B", CorAlvo.Verde, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(5), Fotos(3)).Perfil!;

        _servico.Remove(primeiro);

        Assert.Single(_servico.Perfis);
        Assert.Same(segundo, _servico.Perfis[0]);
    }

    [Fact]
    public void Ids_nao_sao_reaproveitados_entre_cadastros()
    {
        PerfilAlvo primeiro = _servico.Add("A", CorAlvo.Azul, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(5), Fotos(3)).Perfil!;
        PerfilAlvo segundo = _servico.Add("B", CorAlvo.Verde, FormatoAlvo.Quadrado, DimensoesAlvo.Unica(5), Fotos(3)).Perfil!;

        Assert.NotEqual(primeiro.Id, segundo.Id);
    }
}
