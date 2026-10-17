using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Repositories;

/// <summary>
/// Persistência da associação câmera → quadrante (tabela <c>Cameras</c>). Diferente dos demais
/// repositórios de histórico (somente inserção), este é um cadastro: a mesma câmera pode ser
/// reatribuída quantas vezes o operador quiser, então a gravação é um "upsert" pela câmera.
/// </summary>
public interface ICameraQuadranteRepository
{
    IReadOnlyList<CameraQuadrante> GetAll();

    /// <summary>
    /// Cria ou atualiza a associação da câmera informada. <b>Falha</b> (violação do índice
    /// único) se o quadrante já pertencer a outra câmera — a exclusividade é condição do modelo
    /// (um quadrante, uma câmera) e precisa valer inclusive quando duas janelas gravam ao mesmo
    /// tempo, situação em que só o banco consegue decidir quem chegou primeiro.
    /// </summary>
    CameraQuadrante Salvar(CameraQuadrante camera);

    /// <summary>Remove a associação da câmera (volta a "sem quadrante"), se existir.</summary>
    void Remover(string dispositivoId);
}
