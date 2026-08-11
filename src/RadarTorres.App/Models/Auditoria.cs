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

/// <summary>Origem de uma <see cref="AcaoRealizada"/>: quem/o que a disparou.</summary>
public enum OrigemAcao
{
    /// <summary>Disparada manualmente por um usuário (ex.: botão "Acionamento manual").</summary>
    Manual,

    /// <summary>Disparada automaticamente pelo sistema (ex.: modo Localização + acionamento automático).</summary>
    Automatica
}

/// <summary>Resultado final de uma <see cref="AcaoRealizada"/>.</summary>
public enum ResultadoAcao
{
    Executada,
    Cancelada,
    Erro
}

/// <summary>Resultado de uma tentativa de <see cref="AlteracaoModo"/>.</summary>
public enum ResultadoAlteracaoModo
{
    Sucesso,
    Erro
}

/// <summary>Status de acompanhamento de um <see cref="ChamadoAjuda"/>.</summary>
public enum StatusChamado
{
    Aberto,
    EmAnalise,
    Resolvido,
    Cancelado
}
