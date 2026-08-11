using System;
using RadarTorres.App.Helpers;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// ViewModel do formulário de ajuda (Requisito 9). Usuário e data/hora de envio são
/// preenchidos automaticamente a partir da sessão atual — nunca digitados manualmente.
/// </summary>
public sealed class HelpDeskFormViewModel : ViewModelBase
{
    private readonly IChamadoAjudaRepository _repository;
    private readonly IAuthService _authService;
    private readonly ILocalizationService _localizationService;

    public HelpDeskFormViewModel(IChamadoAjudaRepository repository, IAuthService authService, ILocalizationService localizationService)
    {
        _repository = repository;
        _authService = authService;
        _localizationService = localizationService;

        Categorias =
        [
            _localizationService["HelpDesk.Categoria.Duvida"],
            _localizationService["HelpDesk.Categoria.Erro"],
            _localizationService["HelpDesk.Categoria.Sugestao"],
            _localizationService["HelpDesk.Categoria.Outro"],
        ];
        _categoriaSelecionada = Categorias[0];

        EnviarCommand = new RelayCommand(Enviar);
    }

    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string ModuloRelacionado { get; set; } = string.Empty;
    public string MensagemErro { get; set; } = string.Empty;

    public string[] Categorias { get; }

    private string _categoriaSelecionada;
    public string CategoriaSelecionada
    {
        get => _categoriaSelecionada;
        set => SetProperty(ref _categoriaSelecionada, value);
    }

    private string? _mensagem;
    public string? Mensagem
    {
        get => _mensagem;
        set => SetProperty(ref _mensagem, value);
    }

    private bool _enviado;
    public bool Enviado
    {
        get => _enviado;
        set => SetProperty(ref _enviado, value);
    }

    public RelayCommand EnviarCommand { get; }

    private void Enviar()
    {
        if (string.IsNullOrWhiteSpace(Titulo))
        {
            Mensagem = _localizationService["HelpDesk.Error.TituloObrigatorio"];
            return;
        }

        if (string.IsNullOrWhiteSpace(Descricao))
        {
            Mensagem = _localizationService["HelpDesk.Error.DescricaoObrigatoria"];
            return;
        }

        _repository.Add(new ChamadoAjuda
        {
            UsuarioId = _authService.CurrentUser?.Id ?? 0,
            UsuarioNome = _authService.CurrentUser?.Nome ?? "—",
            Titulo = Titulo.Trim(),
            Descricao = Descricao.Trim(),
            Categoria = CategoriaSelecionada,
            ModuloRelacionado = string.IsNullOrWhiteSpace(ModuloRelacionado) ? null : ModuloRelacionado.Trim(),
            MensagemErro = string.IsNullOrWhiteSpace(MensagemErro) ? null : MensagemErro.Trim(),
            DataHoraEnvio = DateTime.Now,
            Status = StatusChamado.Aberto
        });

        Mensagem = _localizationService["HelpDesk.Sucesso"];
        Enviado = true;
    }
}
