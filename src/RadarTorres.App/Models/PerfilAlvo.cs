using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace RadarTorres.App.Models;

/// <summary>
/// Região do objeto dentro de uma foto de referência, em pixels da própria foto (origem no
/// canto superior esquerdo). Desenhada pelo usuário sobre a foto recém-tirada na tela "Perfis
/// de Alvo" — é o que diz ao reconhecimento QUAL objeto da foto observar, em vez de deixar o
/// sistema adivinhar pela cor cadastrada (ver <see cref="Services.AlvoVisualService"/>).
/// </summary>
public sealed record RecorteAlvo(int X, int Y, int Largura, int Altura)
{
    /// <summary>Se o recorte tem área utilizável (um clique sem arrastar não seleciona nada).</summary>
    /// <remarks>Calculado — fora do JSON persistido (<see cref="Services.PerfilAlvoRepository"/>).</remarks>
    [JsonIgnore]
    public bool EhValido => Largura > 0 && Altura > 0;
}

/// <summary>
/// Uma das fotografias de referência de um <see cref="PerfilAlvo"/>: o arquivo em disco e, dentro
/// dele, o recorte do objeto marcado pelo usuário. São várias por perfil (mínimo
/// <see cref="PerfilAlvo.MinimoFotos"/> no cadastro) porque o reconhecimento compara o quadro ao
/// vivo com TODAS elas e fica com a melhor similaridade — ângulos e iluminações diferentes do
/// mesmo objeto é justamente o que derruba a comparação com uma foto única.
/// </summary>
public sealed class FotoReferenciaAlvo : INotifyPropertyChanged
{
    private RecorteAlvo? _recorte;

    /// <summary>Caminho absoluto do arquivo PNG salvo em <c>%LocalAppData%\RadarTorres\PerfisAlvo\Fotos</c>.</summary>
    public string Caminho { get; set; } = string.Empty;

    public DateTime CapturadaEm { get; set; }

    /// <summary>
    /// Região desta foto que contém o objeto a observar, ou <c>null</c> se o usuário não marcou
    /// nada (aí o reconhecimento localiza o objeto na foto por conta própria).
    /// </summary>
    public RecorteAlvo? Recorte
    {
        get => _recorte;
        set
        {
            if (Equals(_recorte, value)) return;
            _recorte = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Recorte)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TemRecorte)));
        }
    }

    /// <summary>Se esta foto já tem o objeto marcado — a tela mostra isso na lista de fotos.</summary>
    [JsonIgnore]
    public bool TemRecorte => Recorte is { EhValido: true };

    public event PropertyChangedEventHandler? PropertyChanged;

    public override string ToString() => System.IO.Path.GetFileName(Caminho);
}

/// <summary>
/// Descreve o que a detecção por visão computacional deve travar ao identificar um alvo: cor
/// (<see cref="Cor"/>), formato (<see cref="Formato"/>), dimensões físicas em centímetros e as
/// fotografias de referência (<see cref="Fotos"/>). O usuário pode salvar vários perfis e
/// escolher qual está <see cref="Selecionado"/> (ativo) a qualquer momento — só um perfil fica
/// selecionado por vez, ver <see cref="Services.IPerfilAlvoService.SelecionarAtivo"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Dimensões.</b> Objeto <see cref="FormatoAlvo.Retangular"/> tem duas dimensões
/// independentes (<see cref="LarguraCm"/> e <see cref="AlturaCm"/>); quadrado e redondo têm uma
/// só (<see cref="TamanhoCm"/> = lado ou diâmetro). Tudo em CENTÍMETROS do objeto real — nunca
/// pixels; a medida em pixels é a caixa detectada no quadro (<see cref="VisionDetection"/>), e as
/// duas representações só se encontram na estimativa de distância, dentro de
/// <see cref="Services.AlvoVisualService"/>. <see cref="TamanhoCm"/> é mantido preenchido também
/// nos retangulares (com a maior das duas dimensões), o que preserva os perfis gravados antes de
/// existirem largura/altura: quem só tem <see cref="TamanhoCm"/> continua válido, e a tela pede o
/// detalhamento apenas quando o perfil for editado.
/// </para>
/// <para>
/// <b>Fotos.</b> Cada foto é capturada de uma das câmeras na tela "Perfis de Alvo" e carrega o
/// recorte do objeto marcado pelo usuário. O reconhecimento compara cada candidato do quadro ao
/// vivo com todas as fotos do perfil (ver <see cref="Services.AlvoVisualService"/>) — o recorte é
/// o que garante que a comparação seja contra o objeto, e não contra o fundo ou a pessoa que o
/// segurava.
/// </para>
/// </remarks>
public sealed class PerfilAlvo : INotifyPropertyChanged
{
    /// <summary>
    /// Quantidade mínima de fotografias válidas para CRIAR um perfil. Checada na tela (botão
    /// desabilitado, ver <see cref="ViewModels.PerfilAlvoViewModel"/>) e de novo no serviço
    /// (<see cref="Services.PerfilAlvoService.Add"/>), para a regra não depender da UI.
    /// </summary>
    public const int MinimoFotos = 3;

    private bool _selecionado;
    private List<FotoReferenciaAlvo> _fotos = new();

    public int Id { get; set; }

    /// <summary>Nome livre dado pelo usuário (ex.: "Bola vermelha pequena").</summary>
    public string Nome { get; set; } = string.Empty;

    public CorAlvo Cor { get; set; }

    public FormatoAlvo Formato { get; set; }

    /// <summary>
    /// Dimensão única do objeto, em centímetros: lado (quadrado) ou diâmetro (redondo). Nos
    /// retangulares guarda a MAIOR entre <see cref="LarguraCm"/> e <see cref="AlturaCm"/> — ver
    /// as observações da classe sobre compatibilidade.
    /// </summary>
    public double TamanhoCm { get; set; }

    /// <summary>Largura (dimensão horizontal) do objeto em centímetros — só para <see cref="FormatoAlvo.Retangular"/>; <c>null</c> nos demais formatos e nos perfis antigos.</summary>
    public double? LarguraCm { get; set; }

    /// <summary>Altura (dimensão vertical) do objeto em centímetros — só para <see cref="FormatoAlvo.Retangular"/>; <c>null</c> nos demais formatos e nos perfis antigos.</summary>
    public double? AlturaCm { get; set; }

    /// <summary>
    /// Fotografias de referência deste perfil, na ordem em que foram capturadas. Fonte da
    /// verdade — os campos legados de foto única só existem para migrar arquivos antigos
    /// (ver <see cref="Services.PerfilAlvoRepository"/>).
    /// </summary>
    public List<FotoReferenciaAlvo> Fotos
    {
        get => _fotos;
        set
        {
            _fotos = value ?? new List<FotoReferenciaAlvo>();
            NotificarFotosAlteradas();
        }
    }

    /// <summary>
    /// Se este é o perfil atualmente ativo (o que a detecção deve travar agora). Só um perfil
    /// fica <c>true</c> por vez — ver <see cref="Services.PerfilAlvoService.SelecionarAtivo"/>.
    /// </summary>
    public bool Selecionado
    {
        get => _selecionado;
        set => SetField(ref _selecionado, value);
    }

    // ------------------------------------------------------------------ Campos legados (migração)
    //
    // Gravados pelas versões que só suportavam UMA foto por perfil. Continuam sendo LIDOS do
    // JSON para que nenhuma foto já cadastrada seja perdida na atualização; PerfilAlvoRepository
    // os converte em uma entrada de Fotos e os zera. Nunca consulte estes campos no código novo.

    [JsonPropertyName("CaminhoFotoReferencia")]
    public string? CaminhoFotoReferenciaLegado { get; set; }

    [JsonPropertyName("RecorteFotoReferencia")]
    public RecorteAlvo? RecorteFotoReferenciaLegado { get; set; }

    // ------------------------------------------------------------------ Derivados (fora do JSON)

    [JsonIgnore]
    public int TotalFotos => Fotos.Count;

    /// <summary>
    /// Primeira foto do perfil, usada quando é preciso UMA imagem representativa (ex.:
    /// <see cref="ObjetoDetectado.ReferenciaImagem"/> e a prévia da lista). <c>null</c> se o
    /// perfil ainda não tem foto.
    /// </summary>
    [JsonIgnore]
    public string? CaminhoFotoReferencia => Fotos.Count > 0 ? Fotos[0].Caminho : null;

    /// <summary>
    /// Maior dimensão física do objeto, em centímetros — é a medida comparável com o maior lado
    /// da caixa detectada, e por isso a que entra na estimativa de distância.
    /// </summary>
    [JsonIgnore]
    public double MaiorDimensaoCm => Formato == FormatoAlvo.Retangular && (LarguraCm is > 0 || AlturaCm is > 0)
        ? Math.Max(LarguraCm ?? 0, AlturaCm ?? 0)
        : TamanhoCm;

    /// <summary>Dimensões em texto para as telas e relatórios (ex.: "12,0 × 7,5 cm" ou "8,0 cm").</summary>
    [JsonIgnore]
    public string DimensoesDescricao => Formato == FormatoAlvo.Retangular && LarguraCm is > 0 && AlturaCm is > 0
        ? $"{LarguraCm:0.0} × {AlturaCm:0.0} cm"
        : $"{TamanhoCm:0.0} cm";

    /// <summary>Cópia independente das fotos, para a tela editar sem alterar o perfil salvo até o usuário confirmar.</summary>
    public List<FotoReferenciaAlvo> ClonarFotos() => Fotos
        .Select(f => new FotoReferenciaAlvo { Caminho = f.Caminho, CapturadaEm = f.CapturadaEm, Recorte = f.Recorte })
        .ToList();

    /// <summary>Avisa as telas de que a lista de fotos mudou no lugar (sem trocar a referência da lista).</summary>
    public void NotificarFotosAlteradas()
    {
        OnPropertyChanged(nameof(Fotos));
        OnPropertyChanged(nameof(TotalFotos));
        OnPropertyChanged(nameof(CaminhoFotoReferencia));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public override string ToString() =>
        $"{Nome} [{Cor}/{Formato}, {DimensoesDescricao}, {TotalFotos} foto(s)]{(Selecionado ? " (ativo)" : "")}";
}
