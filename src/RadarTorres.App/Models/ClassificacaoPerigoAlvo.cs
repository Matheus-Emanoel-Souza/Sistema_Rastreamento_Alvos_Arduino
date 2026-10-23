using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Registro de auditoria de uma mudança de <see cref="GrauPerigoAlvo"/> de um alvo. Gravado por
/// <see cref="ViewModels.MainViewModel"/> só quando o grau MUDA (não a cada leitura) — espelha a
/// tabela <c>classificacoes_alvo</c> descrita em
/// <c>Docs/Documentos_Entregaveis/Banco_de_Dados/schema.sql</c>.
/// </summary>
public class ClassificacaoPerigoAlvo
{
    public int Id { get; set; }

    /// <summary>Referência ao <see cref="ObjetoDetectado"/> deste alvo, quando já persistido. <c>null</c> antes da primeira detecção ser gravada.</summary>
    public int? ObjetoDetectadoId { get; set; }

    public double VelocidadeMetrosPorSegundo { get; set; }

    public GrauPerigoAlvo Grau { get; set; }

    public DateTime DataHora { get; set; }
}
