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
    /// Cria ou atualiza a associação da câmera informada. Libera automaticamente o quadrante de
    /// qualquer OUTRA câmera que o estivesse ocupando — a exclusividade é condição do modelo
    /// (um quadrante, uma câmera) e precisa valer também quando duas janelas gravam ao mesmo
    /// tempo, por isso acontece na mesma transação da gravação.
    /// </summary>
    CameraQuadrante Salvar(CameraQuadrante camera);

    /// <summary>Remove a associação da câmera (volta a "sem quadrante"), se existir.</summary>
    void Remover(string dispositivoId);
}
