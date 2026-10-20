using System.Drawing;
using System.IO;
using System.Drawing.Imaging;
using RadarTorres.App.Configuration;
using Xunit;

// Os testes compartilham estado de processo (AppConfig.Current, que é estático, e a connection
// string única de SqliteConnectionFactory), então rodam em série. Paralelizar aqui produziria
// falhas intermitentes que nada têm a ver com o código testado.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace RadarTorres.Tests;

/// <summary>Arquivos de imagem temporários para exercitar a validação de fotografias.</summary>
internal static class FotosDeTeste
{
    /// <summary>Pasta temporária exclusiva deste teste, apagada por <see cref="Limpar"/>.</summary>
    public static string CriarPasta()
    {
        string pasta = Path.Combine(Path.GetTempPath(), "RadarTorresTestes", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(pasta);
        return pasta;
    }

    public static void Limpar(string pasta)
    {
        try
        {
            if (Directory.Exists(pasta)) Directory.Delete(pasta, recursive: true);
        }
        catch (IOException)
        {
            // Pasta temporária presa por antivírus/indexador não deve derrubar o teste.
        }
    }

    /// <summary>PNG válido, acima do lado mínimo exigido.</summary>
    public static string FotoValida(string pasta, int lado = 160)
    {
        string caminho = Path.Combine(pasta, $"valida-{Guid.NewGuid():N}.png");

        using var bitmap = new Bitmap(lado, lado, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.DarkRed);
            g.FillEllipse(Brushes.Red, lado / 4, lado / 4, lado / 2, lado / 2);
        }
        bitmap.Save(caminho, ImageFormat.Png);

        return caminho;
    }

    /// <summary>Arquivo com extensão de imagem mas conteúdo que não é imagem.</summary>
    public static string ArquivoCorrompido(string pasta)
    {
        string caminho = Path.Combine(pasta, $"corrompida-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(caminho, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x01, 0x02, 0x03 });
        return caminho;
    }

    public static string ArquivoVazio(string pasta)
    {
        string caminho = Path.Combine(pasta, $"vazia-{Guid.NewGuid():N}.png");
        File.WriteAllBytes(caminho, Array.Empty<byte>());
        return caminho;
    }

    /// <summary>PNG legítimo, porém menor que o mínimo aceito pela validação.</summary>
    public static string FotoMinuscula(string pasta)
    {
        string caminho = Path.Combine(pasta, $"minuscula-{Guid.NewGuid():N}.png");
        using var bitmap = new Bitmap(16, 16, PixelFormat.Format24bppRgb);
        bitmap.Save(caminho, ImageFormat.Png);
        return caminho;
    }
}

/// <summary>Restaura <see cref="AppConfig.Current"/> ao sair, para um teste não vazar configuração no outro.</summary>
internal sealed class ConfiguracaoDeTeste : IDisposable
{
    private readonly AppSettings _anterior;

    public ConfiguracaoDeTeste(Action<VisionSettings> ajustar)
    {
        _anterior = AppConfig.Current;

        var nova = new AppSettings();
        ajustar(nova.VisionSettings);
        AppConfig.Current = nova;
    }

    public void Dispose() => AppConfig.Current = _anterior;
}
