namespace RadarTorres.App.Models;

/// <summary>Perfil/nível de permissão de um <see cref="Usuario"/>.</summary>
public enum PerfilUsuario
{
    /// <summary>Acesso completo: gerencia usuários, todas as telas e ações.</summary>
    Administrador,

    /// <summary>Acompanha o sistema e solicita ações permitidas (sem gerenciar usuários).</summary>
    Operador,

    /// <summary>Apenas consulta os dados; nenhuma ação de escrita é permitida.</summary>
    Visualizador
}
