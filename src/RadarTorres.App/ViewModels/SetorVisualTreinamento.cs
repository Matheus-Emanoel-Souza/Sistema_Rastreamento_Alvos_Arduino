using System.Windows.Media;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// O setor (cone) de campo de visão de uma torre, já em coordenadas de tela — um leque angular
/// de <see cref="TreinamentoControladorViewModel.AnguloCampoDeVisaoGraus"/> graus, com raio
/// igual ao alcance de engajamento. Acompanha o alvo mirado (gira para apontar para ele a cada
/// quadro) quando a torre está comprometida com um; volta para a direção padrão (voltada para
/// fora da base) quando livre — ver <see cref="TreinamentoControladorViewModel.AtualizarCamposDeVisao"/>.
/// </summary>
/// <remarks>
/// <see cref="Pontos"/> é SUBSTITUÍDO (nunca mutado no lugar) a cada atualização — mutar uma
/// <see cref="PointCollection"/> já atribuída a um <c>Polygon</c> via <c>{Binding}</c> não é
/// garantia de redesenho (mesmo motivo por que o gráfico de fitness, no code-behind desta tela,
/// também troca a coleção inteira em vez de só adicionar pontos). Por isso implementa
/// <c>INotifyPropertyChanged</c> (via <see cref="ViewModelBase"/>), diferente de
/// <see cref="LinhaVisualTreinamento"/>/<see cref="PontoVisualTreinamento"/>, cujas propriedades
/// são valores simples (double/string) que já notificam normalmente.
/// </remarks>
public sealed class SetorVisualTreinamento : ViewModelBase
{
    public int Id { get; init; }

    private PointCollection _pontos = new();
    public PointCollection Pontos { get => _pontos; set => SetProperty(ref _pontos, value); }

    private string _cor = "#3B82F6";
    public string Cor { get => _cor; set => SetProperty(ref _cor, value); }
}
