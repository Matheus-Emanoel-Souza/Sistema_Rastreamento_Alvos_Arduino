using System.Threading.Tasks;
using RadarTorres.App.Models;
using RadarTorres.App.Services;
using RadarTorres.App.ViewModels;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// Cadastro de alvo no modal com etapas (critérios de aceitação 10 a 15): abertura, navegação
/// preservando os dados, validações por etapa e conclusão.
/// </summary>
/// <remarks>
/// Exercita a ViewModel diretamente, sem WPF: as fotografias são acrescentadas a
/// <see cref="PerfilAlvoViewModel.FormFotos"/> como a captura faria, evitando depender de câmera
/// e de imagem em memória. O que está sob teste é o fluxo do assistente, não o driver de vídeo.
/// </remarks>
public sealed class CadastroAlvoModalTests : IDisposable
{
    private readonly string _pasta = FotosDeTeste.CriarPasta();
    private readonly PerfilAlvoService _perfilService;
    private readonly PerfilAlvoViewModel _vm;

    public CadastroAlvoModalTests()
    {
        _perfilService = new PerfilAlvoService(new RepositorioPerfisEmMemoria(), new LoggerFalso());

        _vm = new PerfilAlvoViewModel(
            _perfilService,
            new PermissionService(),
            new AuthServiceFalso(PerfilUsuario.Administrador),
            new EnumeradorCamerasVazio(),
            new FabricaCapturaFalsa(),
            new LoggerFalso());
    }

    public void Dispose() => FotosDeTeste.Limpar(_pasta);

    private void Fotografar(int quantidade = 3)
    {
        for (int i = 0; i < quantidade; i++)
        {
            _vm.FormFotos.Add(new FotoReferenciaAlvo { Caminho = FotosDeTeste.FotoValida(_pasta), CapturadaEm = DateTime.Now });
        }
    }

    /// <summary>Preenche o rascunho inteiro de um alvo redondo válido.</summary>
    private void PreencherCadastroValido(string nome = "Bola vermelha")
    {
        _vm.AbrirCadastroCommand.Execute(null);
        _vm.FormNome = nome;
        _vm.FormFormato = FormatoAlvo.Redondo;
        Fotografar();
        _vm.FormTamanhoCm = "8";
    }

    // ---------------------------------------------------------------- 10: abertura

    [Fact]
    public void Tela_abre_sem_formulario_e_o_botao_abre_o_modal_na_primeira_etapa()
    {
        Assert.False(_vm.ModalAberto);
        Assert.True(_vm.AbrirCadastroCommand.CanExecute(null));

        _vm.AbrirCadastroCommand.Execute(null);

        Assert.True(_vm.ModalAberto);
        Assert.Equal(EtapaCadastroAlvo.Informacoes, _vm.EtapaAtual);
        Assert.Contains("Etapa 1 de 4", _vm.RotuloEtapa);

        // Com o modal já aberto, o botão não abre um segundo cadastro por cima.
        Assert.False(_vm.AbrirCadastroCommand.CanExecute(null));
    }

    [Fact]
    public void Modal_abre_em_branco_mesmo_depois_de_um_cadastro_anterior()
    {
        PreencherCadastroValido();
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.ConcluirCommand.Execute(null);

        _vm.AbrirCadastroCommand.Execute(null);

        Assert.Equal(string.Empty, _vm.FormNome);
        Assert.Empty(_vm.FormFotos);
        Assert.Equal(EtapaCadastroAlvo.Informacoes, _vm.EtapaAtual);
    }

    // ---------------------------------------------------------------- 12 e 13: etapas e validação

    [Fact]
    public void Nao_avanca_da_primeira_etapa_sem_nome()
    {
        _vm.AbrirCadastroCommand.Execute(null);

        Assert.False(_vm.PodeAvancar);
        Assert.Contains("nome", _vm.PendenciaEtapa!, StringComparison.OrdinalIgnoreCase);

        _vm.AvancarCommand.Execute(null);
        Assert.Equal(EtapaCadastroAlvo.Informacoes, _vm.EtapaAtual);

        _vm.FormNome = "Alvo";
        Assert.True(_vm.PodeAvancar);
        Assert.Null(_vm.PendenciaEtapa);
    }

    [Fact]
    public void Nao_avanca_da_etapa_de_fotos_sem_o_minimo_exigido()
    {
        _vm.AbrirCadastroCommand.Execute(null);
        _vm.FormNome = "Alvo";
        _vm.AvancarCommand.Execute(null);

        Assert.Equal(EtapaCadastroAlvo.Fotografias, _vm.EtapaAtual);

        Fotografar(2);
        Assert.False(_vm.PodeAvancar);
        Assert.Contains("1", _vm.PendenciaEtapa!);

        Fotografar(1);
        Assert.True(_vm.PodeAvancar);
    }

    [Fact]
    public void Nao_avanca_da_etapa_de_dimensoes_sem_medida_valida()
    {
        _vm.AbrirCadastroCommand.Execute(null);
        _vm.FormNome = "Caderno";
        _vm.FormFormato = FormatoAlvo.Retangular;
        Fotografar();

        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        Assert.Equal(EtapaCadastroAlvo.Dimensoes, _vm.EtapaAtual);

        Assert.False(_vm.PodeAvancar);

        _vm.FormLarguraCm = "21";
        Assert.False(_vm.PodeAvancar);          // retangular exige as DUAS medidas

        _vm.FormAlturaCm = "0";
        Assert.False(_vm.PodeAvancar);          // zero não é medida válida

        _vm.FormAlturaCm = "29,7";
        Assert.True(_vm.PodeAvancar);
    }

    [Fact]
    public void Navegar_entre_etapas_preserva_o_que_ja_foi_preenchido()
    {
        PreencherCadastroValido("Bola grande");

        _vm.AvancarCommand.Execute(null);   // fotografias
        _vm.AvancarCommand.Execute(null);   // dimensões
        _vm.VoltarCommand.Execute(null);    // volta para fotografias
        _vm.VoltarCommand.Execute(null);    // volta para informações

        Assert.Equal(EtapaCadastroAlvo.Informacoes, _vm.EtapaAtual);
        Assert.Equal("Bola grande", _vm.FormNome);
        Assert.Equal(3, _vm.FormFotos.Count);
        Assert.Equal("8", _vm.FormTamanhoCm);

        // E volta para a frente sem perder nada.
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        Assert.Equal(EtapaCadastroAlvo.Revisao, _vm.EtapaAtual);
        Assert.Contains("Bola grande", _vm.ResumoRevisao);
        Assert.Contains("3", _vm.ResumoRevisao);
    }

    [Fact]
    public void Primeira_etapa_nao_tem_para_onde_voltar()
    {
        _vm.AbrirCadastroCommand.Execute(null);

        Assert.False(_vm.VoltarCommand.CanExecute(null));
    }

    [Fact]
    public void Conclusao_so_fica_disponivel_na_ultima_etapa_e_com_tudo_valido()
    {
        _vm.AbrirCadastroCommand.Execute(null);
        _vm.FormNome = "Alvo";

        Assert.False(_vm.EhUltimaEtapa);
        Assert.False(_vm.ConcluirCommand.CanExecute(null));   // faltam fotos e dimensões

        Fotografar();
        _vm.FormTamanhoCm = "10";

        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);

        Assert.True(_vm.EhUltimaEtapa);
        Assert.True(_vm.ConcluirCommand.CanExecute(null));
    }

    // ---------------------------------------------------------------- 14: conclusão

    [Fact]
    public void Cadastro_concluido_fecha_o_modal_e_atualiza_a_listagem()
    {
        PreencherCadastroValido("Bola vermelha");
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);

        _vm.ConcluirCommand.Execute(null);

        Assert.False(_vm.ModalAberto);
        Assert.Single(_vm.Perfis);
        Assert.Equal("Bola vermelha", _vm.Perfis[0].Nome);
        Assert.Same(_vm.Perfis[0], _vm.SelectedPerfil);
        Assert.True(_vm.SucessoFormulario);

        // Rascunho limpo para o próximo cadastro.
        Assert.Equal(string.Empty, _vm.FormNome);
        Assert.Empty(_vm.FormFotos);
    }

    [Fact]
    public void Concluir_duas_vezes_seguidas_nao_duplica_o_alvo()
    {
        PreencherCadastroValido();
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);

        _vm.ConcluirCommand.Execute(null);
        _vm.ConcluirCommand.Execute(null);   // clique repetido

        Assert.Single(_vm.Perfis);
    }

    // ---------------------------------------------------------------- 15: erro preserva o rascunho

    [Fact]
    public void Falha_na_gravacao_mantem_o_modal_aberto_e_os_dados_preenchidos()
    {
        _vm.AbrirCadastroCommand.Execute(null);
        _vm.FormNome = "Alvo com foto ruim";
        _vm.FormTamanhoCm = "10";

        // Duas fotos boas e uma corrompida: a tela deixa avançar (são 3 arquivos), mas o
        // serviço recusa na gravação — é o caso que não pode apagar o que o usuário preencheu.
        Fotografar(2);
        _vm.FormFotos.Add(new FotoReferenciaAlvo { Caminho = FotosDeTeste.ArquivoCorrompido(_pasta) });

        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.ConcluirCommand.Execute(null);

        Assert.True(_vm.ModalAberto);
        Assert.Empty(_vm.Perfis);
        Assert.False(_vm.SucessoFormulario);
        Assert.Contains("inválida", _vm.MensagemFormulario!, StringComparison.OrdinalIgnoreCase);

        // Nada foi perdido: o usuário remove a foto ruim e conclui.
        Assert.Equal("Alvo com foto ruim", _vm.FormNome);
        Assert.Equal(3, _vm.FormFotos.Count);

        _vm.RemoverFotoCommand.Execute(_vm.FormFotos[2]);
        Fotografar(1);
        _vm.ConcluirCommand.Execute(null);

        Assert.False(_vm.ModalAberto);
        Assert.Single(_vm.Perfis);
    }

    // ---------------------------------------------------------------- Edição pelo mesmo modal

    [Fact]
    public void Editar_abre_o_modal_com_os_dados_do_alvo_e_nao_cria_registro_novo()
    {
        PreencherCadastroValido("Alvo original");
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.ConcluirCommand.Execute(null);

        int idOriginal = _vm.Perfis[0].Id;
        _vm.SelectedPerfil = _vm.Perfis[0];

        _vm.EditarCommand.Execute(null);

        Assert.True(_vm.ModalAberto);
        Assert.True(_vm.EmEdicao);
        Assert.Equal(EtapaCadastroAlvo.Informacoes, _vm.EtapaAtual);
        Assert.Equal("Alvo original", _vm.FormNome);
        Assert.Equal(3, _vm.FormFotos.Count);
        Assert.Contains("Editando", _vm.TituloFormulario);

        _vm.FormNome = "Alvo renomeado";
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.ConcluirCommand.Execute(null);

        Assert.Single(_vm.Perfis);
        Assert.Equal(idOriginal, _vm.Perfis[0].Id);
        Assert.Equal("Alvo renomeado", _vm.Perfis[0].Nome);
        Assert.False(_vm.ModalAberto);
    }

    [Fact]
    public void Edicao_dispensa_o_minimo_de_fotos_para_avancar()
    {
        PreencherCadastroValido();
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.AvancarCommand.Execute(null);
        _vm.ConcluirCommand.Execute(null);

        _vm.SelectedPerfil = _vm.Perfis[0];
        _vm.EditarCommand.Execute(null);

        _vm.RemoverFotoCommand.Execute(_vm.FormFotos[0]);
        _vm.AvancarCommand.Execute(null);

        Assert.Equal(EtapaCadastroAlvo.Fotografias, _vm.EtapaAtual);
        Assert.True(_vm.PodeAvancar);
    }

    // ---------------------------------------------------------------- Fechar / descartar

    [Fact]
    public void Fechar_o_modal_descarta_o_rascunho_sem_gravar_nada()
    {
        PreencherCadastroValido("Será descartado");

        Assert.True(_vm.TemDadosPreenchidos);

        _vm.FecharModalCommand.Execute(null);

        Assert.False(_vm.ModalAberto);
        Assert.Empty(_vm.Perfis);
        Assert.Equal(string.Empty, _vm.FormNome);
        Assert.False(_vm.TemDadosPreenchidos);
    }

    [Fact]
    public void Modal_recem_aberto_nao_tem_dados_a_descartar()
    {
        _vm.AbrirCadastroCommand.Execute(null);

        // A View só pede confirmação quando há algo preenchido.
        Assert.False(_vm.TemDadosPreenchidos);
    }

    // ---------------------------------------------------------------- Permissão

    [Fact]
    public void Visualizador_nao_pode_abrir_o_cadastro()
    {
        var somenteLeitura = new PerfilAlvoViewModel(
            new PerfilAlvoService(new RepositorioPerfisEmMemoria(), new LoggerFalso()),
            new PermissionService(),
            new AuthServiceFalso(PerfilUsuario.Visualizador),
            new EnumeradorCamerasVazio(),
            new FabricaCapturaFalsa(),
            new LoggerFalso());

        Assert.False(somenteLeitura.PodeGerenciar);
        Assert.False(somenteLeitura.AbrirCadastroCommand.CanExecute(null));
    }
}

// ---------------------------------------------------------------------- Dublês desta suíte

internal sealed class AuthServiceFalso : IAuthService
{
    public AuthServiceFalso(PerfilUsuario perfil) =>
        CurrentUser = new Usuario { Id = 1, Nome = "Teste", Login = "teste", Perfil = perfil, Ativo = true };

    public Usuario? CurrentUser { get; private set; }

    public event EventHandler? SessionChanged;

    public Task<AuthResult> LoginAsync(string login, string senha) => Task.FromResult(AuthResult.Ok(CurrentUser!));

    public void Logout()
    {
        CurrentUser = null;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<AuthResult> AlterarSenhaAsync(string senhaAtual, string novaSenha) => Task.FromResult(AuthResult.Ok(CurrentUser!));

    public Task<AuthResult> RedefinirSenhaAsync(int usuarioId, string novaSenha) => Task.FromResult(AuthResult.Ok(CurrentUser!));
}

internal sealed class EnumeradorCamerasVazio : ICameraDeviceEnumerator
{
    public IReadOnlyList<CameraDevice> GetAvailableCameras() => Array.Empty<CameraDevice>();
}

internal sealed class CapturaFalsa : ICameraCaptureService
{
    public bool IsRunning { get; private set; }

    public CameraDevice? CurrentDevice { get; private set; }

    public event EventHandler<CameraFrameCapturedEventArgs>? FrameCaptured;
    public event EventHandler<CameraErrorEventArgs>? ErrorOccurred;
    public event EventHandler? Stopped;

    public void Start(CameraDevice device)
    {
        CurrentDevice = device;
        IsRunning = true;
    }

    public void Stop()
    {
        IsRunning = false;
        CurrentDevice = null;
        Stopped?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => Stop();

    /// <summary>Mantidos para a interface — nenhum teste desta suíte simula quadros.</summary>
    internal void NaoUsado()
    {
        FrameCaptured?.Invoke(this, null!);
        ErrorOccurred?.Invoke(this, null!);
    }
}

internal sealed class FabricaCapturaFalsa : ICameraCaptureServiceFactory
{
    public ICameraCaptureService Create() => new CapturaFalsa();
}
