using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Associação persistida entre uma câmera/webcam e o quadrante que ela é responsável por
/// monitorar (ver <see cref="Helpers.QuadrantHelper"/> para a convenção Q1..Q4). É o que permite
/// a uma detecção por câmera registrar o quadrante da OCORRÊNCIA a partir de quem a enxergou, em
/// vez de inferir pela posição estimada do objeto.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DispositivoId"/> é o moniker DirectShow do <see cref="CameraDevice.Id"/> — estável
/// para o mesmo dispositivo na mesma máquina, que é o escopo do banco (um <c>radartorres.db</c>
/// por usuário do Windows, ver <see cref="Data.AppDataPaths"/>). <see cref="Nome"/> é guardado
/// junto só para a listagem continuar legível quando a câmera estiver desconectada — o vínculo
/// nunca depende do nome.
/// </para>
/// <para>
/// <b>Regra:</b> cada câmera responde por exatamente UM quadrante, e cada quadrante tem no
/// máximo UMA câmera (decisão do usuário registrada em <c>Docs/Projeto/LOG_SOLICITACOES.md</c>) —
/// garantido pelo serviço e também por índices únicos no banco, ver
/// <see cref="Repositories.SqliteCameraQuadranteRepository"/>.
/// </para>
/// </remarks>
public sealed class CameraQuadrante
{
    public int Id { get; set; }

    /// <summary>Identificador estável do dispositivo (<see cref="CameraDevice.Id"/>).</summary>
    public string DispositivoId { get; set; } = string.Empty;

    /// <summary>Nome amigável da câmera no momento em que a associação foi salva.</summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>Quadrante monitorado. Nunca <see cref="Quadrant.None"/> em registro persistido — "sem quadrante" é a ausência da linha.</summary>
    public Quadrant Quadrante { get; set; }

    public DateTime AtualizadoEm { get; set; }

    public override string ToString() => $"{Nome} → {Helpers.QuadrantHelper.ToDisplayLabel(Quadrante)}";
}
