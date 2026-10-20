using System;
using System.Drawing;
using System.IO;

namespace RadarTorres.App.Helpers;

/// <summary>
/// Decide se um arquivo serve como fotografia de referência de um perfil de alvo. Centralizado
/// aqui porque a mesma regra é aplicada em dois lugares de propósito: na tela, para o usuário
/// saber na hora que a foto não entrou (e o botão de cadastrar continuar desabilitado), e dentro
/// de <see cref="Services.PerfilAlvoService"/>, para a exigência de no mínimo
/// <see cref="Models.PerfilAlvo.MinimoFotos"/> fotos não depender da interface — chamada direta
/// ao serviço com uma lista incompleta é recusada do mesmo jeito.
/// </summary>
/// <remarks>
/// A validação ABRE a imagem em vez de só olhar a extensão: arquivo truncado pela metade (câmera
/// desconectada no meio da gravação) tem extensão .png e tamanho não-zero, mas quebraria depois,
/// dentro do reconhecimento, onde o erro não teria mais relação visível com a foto escolhida.
/// </remarks>
public static class ValidacaoFotoAlvo
{
    /// <summary>
    /// Lado mínimo aceito, em pixels. Abaixo disso não há informação suficiente para a
    /// assinatura visual, que reamostra a região do objeto para 32×32 (ver
    /// <see cref="Services.AlvoVisualService"/>) — uma foto menor que isso só produziria
    /// borrão ampliado.
    /// </summary>
    public const int LadoMinimoPx = 64;

    /// <summary>
    /// <c>true</c> se o arquivo existe, abre como imagem e tem pelo menos
    /// <see cref="LadoMinimoPx"/> pixels em cada lado. <paramref name="motivo"/> traz o texto
    /// pronto para exibição quando for inválida.
    /// </summary>
    public static bool EhValida(string? caminho, out string motivo)
    {
        if (string.IsNullOrWhiteSpace(caminho))
        {
            motivo = "Caminho da foto vazio.";
            return false;
        }

        if (!File.Exists(caminho))
        {
            motivo = $"Arquivo não encontrado: {Path.GetFileName(caminho)}.";
            return false;
        }

        try
        {
            if (new FileInfo(caminho).Length == 0)
            {
                motivo = $"Arquivo vazio: {Path.GetFileName(caminho)}.";
                return false;
            }

            // FromStream (e não FromFile) para o arquivo não continuar preso ao processo: a
            // mesma foto é logo depois carregada pela tela e pelo reconhecimento.
            using FileStream stream = File.OpenRead(caminho);
            using Image imagem = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);

            if (imagem.Width < LadoMinimoPx || imagem.Height < LadoMinimoPx)
            {
                motivo = $"Imagem muito pequena ({imagem.Width}×{imagem.Height} px) — mínimo {LadoMinimoPx}×{LadoMinimoPx}.";
                return false;
            }

            motivo = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            // Image.FromStream lança OutOfMemoryException (não uma exceção de formato) para
            // arquivo corrompido ou que não é imagem — por isso o catch é amplo de propósito.
            motivo = $"Arquivo inválido ou corrompido ({Path.GetFileName(caminho)}): {ex.GetType().Name}.";
            return false;
        }
    }

    /// <summary>Atalho para quando só interessa o veredito.</summary>
    public static bool EhValida(string? caminho) => EhValida(caminho, out _);
}
