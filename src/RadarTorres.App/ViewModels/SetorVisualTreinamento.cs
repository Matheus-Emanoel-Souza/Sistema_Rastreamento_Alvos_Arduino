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
/// <see cref="Pontos"/> é mutado no lugar (<c>Clear</c>+<c>Add</c>), nunca substituído — é assim
/// que o <c>Polygon</c> ligado a ele percebe a mudança sem precisar de
/// <c>INotifyPropertyChanged</c>: <see cref="PointCollection"/> é um <c>Freezable</c> que
/// invalida o desenho sozinho quando seu conteúdo muda.
/// </remarks>
public sealed class SetorVisualTreinamento
{
    public int Id { get; init; }
    public PointCollection Pontos { get; } = new();
    public string Cor { get; set; } = "#3B82F6";
}
