namespace RadarTorres.App.Configuration;

/// <summary>
/// Ponto de acesso estático e único à configuração carregada de <c>appsettings.json</c>
/// no início da aplicação (ver <c>App.xaml.cs</c>). Mantido deliberadamente simples
/// (sem um container de injeção de dependências completo) porque o projeto é de porte
/// pequeno/médio; caso o projeto cresça, este é o ponto natural para migrar para
/// <c>Microsoft.Extensions.DependencyInjection</c>.
/// </summary>
public static class AppConfig
{
    /// <summary>Instância única de configuração, disponível assim que a aplicação inicia.</summary>
    public static AppSettings Current { get; set; } = new();
}
