using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>Persistência dos perfis de alvo configurados (independente de usuário — decisão
/// operacional da instalação, mesmo espírito de <see cref="IZonaMortaRepository"/>).</summary>
public interface IPerfilAlvoRepository
{
    List<PerfilAlvo> Load();

    void Save(List<PerfilAlvo> perfis);
}
