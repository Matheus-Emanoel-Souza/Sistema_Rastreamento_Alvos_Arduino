using RadarTorres.App.NeuralEvolution;

namespace RadarTorres.App.ViewModels;

/// <summary>
/// Uma espécie da população, pronta para a tela: além dos números de
/// <see cref="ResumoEspecie"/>, carrega a largura da barra proporcional ao tamanho da espécie e a
/// cor que destaca aquela de onde saiu o campeão.
/// </summary>
/// <remarks>
/// A especiação é o mecanismo que o método anterior não tinha: ali a população era um bloco único,
/// e não havia subdivisão alguma para observar. Ver uma espécie crescer enquanto outra encolhe é o
/// jeito mais direto de entender se a evolução está explorando alternativas ou convergindo.
/// </remarks>
public sealed class LinhaEspecieTreinamento
{
    public required int Id { get; init; }
    public required int Tamanho { get; init; }
    public required double FitnessMedio { get; init; }
    public required int TamanhoElite { get; init; }
    public required int Filhotes { get; init; }
    public required double ComplexidadeMedia { get; init; }
    public required bool ContemCampeao { get; init; }

    /// <summary>Largura da barra em pixels, proporcional ao tamanho da maior espécie da geração.</summary>
    public required double LarguraBarraPx { get; init; }

    public string Cor => ContemCampeao ? "#F59E0B" : "#3B82F6";

    /// <summary>Rótulo da espécie com a marca de quem contém o campeão.</summary>
    public string Rotulo => ContemCampeao ? $"#{Id} ★" : $"#{Id}";

    public string DescricaoCompleta =>
        $"Espécie #{Id}{(ContemCampeao ? " — contém o campeão da população" : string.Empty)}\n" +
        $"Indivíduos: {Tamanho}\n" +
        $"Fitness médio: {FitnessMedio:0.0}\n" +
        $"Elite preservada: {TamanhoElite}\n" +
        $"Filhotes concedidos: {Filhotes}\n" +
        $"Complexidade média: {ComplexidadeMedia:0.0} conexões";

    /// <summary>Converte o retrato vindo da evolução, dimensionando a barra pela maior espécie da geração.</summary>
    public static LinhaEspecieTreinamento De(ResumoEspecie resumo, int maiorTamanho, double larguraMaximaPx)
    {
        double proporcao = maiorTamanho > 0 ? resumo.Tamanho / (double)maiorTamanho : 0.0;

        return new LinhaEspecieTreinamento
        {
            Id = resumo.Id,
            Tamanho = resumo.Tamanho,
            FitnessMedio = resumo.FitnessMedio,
            TamanhoElite = resumo.TamanhoElite,
            Filhotes = resumo.Filhotes,
            ComplexidadeMedia = resumo.ComplexidadeMedia,
            ContemCampeao = resumo.ContemCampeao,
            // Piso de 2px para uma espécie minúscula continuar visível como barra, em vez de
            // virar um traço invisível que parece ausência de dado.
            LarguraBarraPx = resumo.Tamanho > 0 ? System.Math.Max(2.0, proporcao * larguraMaximaPx) : 0.0
        };
    }
}
