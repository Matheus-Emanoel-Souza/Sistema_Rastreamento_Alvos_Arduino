using System;
using System.Collections.Generic;
using RadarTorres.App.Models;

namespace RadarTorres.App.Services;

/// <summary>
/// Estado de UM alvo num instante da simulação, no formato em que ele será entregue ao sistema
/// físico. Coordenadas em metros do mundo simulado — nunca em pixels de tela, que não têm
/// significado nenhum para o hardware.
/// </summary>
/// <param name="AlturaMetros">Altura do alvo (eixo Z); 0 quando a simulação roda em duas dimensões.</param>
/// <param name="VisivelPorTodasAsTorres">Se a altura o torna visível para todas as torres — ver <see cref="NeuralEvolution.ParametrosTreinamento.AlturaVisivelPorTodasAsTorresMetros"/>.</param>
/// <param name="TorreComprometidaId">Torre que assumiu este alvo, ou <c>null</c> se nenhuma ainda.</param>
/// <param name="Certeza">Certeza atual (0 a 1) de que isto é um alvo — mesma grandeza que o sistema real recebe da câmera.</param>
public sealed record AlvoTelemetria(
    int Id,
    double X,
    double Y,
    double AlturaMetros,
    double VelocidadeMetrosPorSegundo,
    Quadrant Quadrante,
    bool VisivelPorTodasAsTorres,
    int? TorreComprometidaId,
    double Certeza);

/// <summary>
/// Um quadro de telemetria: a fotografia completa dos alvos num instante da simulação, com os
/// dois relógios necessários para alinhar o mundo virtual ao físico.
/// </summary>
/// <param name="TempoSimulacaoSegundos">
/// Relógio do EPISÓDIO — quanto tempo simulado se passou desde que ele começou. É este que define
/// a ordem e o espaçamento dos acontecimentos.
/// </param>
/// <param name="DataHora">
/// Relógio de PAREDE no momento em que o quadro foi produzido. Guardado junto porque é o que
/// permite verificar, depois, se a simulação de fato correu em tempo normal: com os dois relógios
/// lado a lado, qualquer defasagem entre o tempo virtual e o real fica medível em vez de
/// presumida.
/// </param>
/// <param name="EixoZHabilitado">Se este quadro foi produzido com a terceira dimensão ligada.</param>
public sealed record QuadroTelemetriaSimulacao(
    double TempoSimulacaoSegundos,
    DateTime DataHora,
    bool EixoZHabilitado,
    IReadOnlyList<AlvoTelemetria> Alvos);

/// <summary>
/// Ponto de entrega dos dados da simulação para quem quiser consumi-los — hoje, nada além da
/// própria tela; amanhã, a ponte serial com o Arduino.
/// </summary>
/// <remarks>
/// <para>
/// Existe para que a preparação da integração física não exija mexer na simulação depois: a tela
/// de treinamento já publica um quadro por passo da prévia, e quem for implementar o envio ao
/// hardware só precisa assinar <see cref="QuadroPublicado"/> ou ler <see cref="UltimoQuadro"/> —
/// sem nenhuma alteração em <see cref="NeuralEvolution.AmbienteTreinamento"/>.
/// </para>
/// <para>
/// <b>A integração física NÃO está concluída.</b> Nada aqui escreve na porta serial. O que este
/// contrato garante é que os dados existem, estão atualizados e carregam a referência temporal
/// necessária para serem enviados em tempo real quando essa etapa for feita.
/// </para>
/// <para>
/// Só a prévia visual publica quadros, e é o correto: ela roda em tempo normal (um passo de
/// <c>dt</c> por tique do relógio), que é a condição do experimento. O laço de treinamento roda o
/// mais rápido possível de propósito — são milhares de episódios por minuto para avaliar fitness,
/// e publicá-los inundaria o consumidor com um tempo simulado que não corresponde a nada no mundo
/// físico.
/// </para>
/// </remarks>
public interface ITelemetriaSimulacaoService
{
    /// <summary>Último quadro publicado, ou <c>null</c> se a simulação ainda não produziu nenhum.</summary>
    QuadroTelemetriaSimulacao? UltimoQuadro { get; }

    /// <summary>Disparado a cada novo quadro — o gancho para a futura transmissão ao hardware.</summary>
    event EventHandler<QuadroTelemetriaSimulacao>? QuadroPublicado;

    /// <summary>Publica um quadro novo, substituindo <see cref="UltimoQuadro"/>.</summary>
    void Publicar(QuadroTelemetriaSimulacao quadro);

    /// <summary>Descarta o último quadro — usado quando a simulação para, para não deixar dados velhos passando por atuais.</summary>
    void Limpar();
}
