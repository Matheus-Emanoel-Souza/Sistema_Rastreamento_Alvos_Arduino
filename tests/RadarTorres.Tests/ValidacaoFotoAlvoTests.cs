using System.IO;
using RadarTorres.App.Helpers;
using Xunit;

namespace RadarTorres.Tests;

/// <summary>
/// "Fotografia válida" (critérios 1 e 2): arquivo inexistente, vazio, corrompido ou abaixo do
/// tamanho mínimo não conta para o mínimo exigido no cadastro.
/// </summary>
public sealed class ValidacaoFotoAlvoTests : IDisposable
{
    private readonly string _pasta = FotosDeTeste.CriarPasta();

    public void Dispose() => FotosDeTeste.Limpar(_pasta);

    [Fact]
    public void Png_acima_do_minimo_e_valido()
    {
        Assert.True(ValidacaoFotoAlvo.EhValida(FotosDeTeste.FotoValida(_pasta), out string motivo), motivo);
        Assert.Equal(string.Empty, motivo);
    }

    [Fact]
    public void Caminho_vazio_ou_nulo_e_invalido()
    {
        Assert.False(ValidacaoFotoAlvo.EhValida(null));
        Assert.False(ValidacaoFotoAlvo.EhValida(string.Empty));
        Assert.False(ValidacaoFotoAlvo.EhValida("   "));
    }

    [Fact]
    public void Arquivo_inexistente_e_invalido()
    {
        string inexistente = Path.Combine(_pasta, "nao-existe.png");

        Assert.False(ValidacaoFotoAlvo.EhValida(inexistente, out string motivo));
        Assert.Contains("não encontrado", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Arquivo_vazio_e_invalido()
    {
        Assert.False(ValidacaoFotoAlvo.EhValida(FotosDeTeste.ArquivoVazio(_pasta), out string motivo));
        Assert.Contains("vazio", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Arquivo_corrompido_e_invalido()
    {
        Assert.False(ValidacaoFotoAlvo.EhValida(FotosDeTeste.ArquivoCorrompido(_pasta), out string motivo));
        Assert.Contains("inválido", motivo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Imagem_menor_que_o_minimo_e_invalida()
    {
        Assert.False(ValidacaoFotoAlvo.EhValida(FotosDeTeste.FotoMinuscula(_pasta), out string motivo));
        Assert.Contains("pequena", motivo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// A validação não pode deixar o arquivo preso: a mesma foto é carregada em seguida pela tela
    /// e pelo reconhecimento.
    /// </summary>
    [Fact]
    public void Validacao_nao_mantem_o_arquivo_bloqueado()
    {
        string caminho = FotosDeTeste.FotoValida(_pasta);

        Assert.True(ValidacaoFotoAlvo.EhValida(caminho));

        using FileStream stream = File.Open(caminho, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(stream.Length > 0);
    }
}
