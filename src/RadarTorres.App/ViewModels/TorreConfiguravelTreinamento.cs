namespace RadarTorres.App.ViewModels;

/// <summary>
/// Uma linha da lista "Torres desativadas" da janela de configurar experimento — espelha uma
/// torre do layout físico (<see cref="Configuration.AppConfig.Towers"/>) com um único campo
/// editável: se ela deve ser excluída da seleção neste treinamento (ver
/// <see cref="NeuralEvolution.ParametrosTreinamento.TorresDesativadasIds"/>). Simula o sistema
/// operando com menos recursos sem precisar remover a torre do layout físico configurado.
/// </summary>
public sealed class TorreConfiguravelTreinamento : ViewModelBase
{
    public int Id { get; init; }
    public string Nome { get; init; } = string.Empty;

    private bool _desativada;
    public bool Desativada { get => _desativada; set => SetProperty(ref _desativada, value); }
}
