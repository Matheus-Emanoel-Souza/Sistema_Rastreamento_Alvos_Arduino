namespace RadarTorres.App.Models;

/// <summary>
/// Uma câmera/webcam reconhecida pelo Windows (módulo Câmeras). <see cref="Id"/> é o
/// identificador estável do dispositivo (moniker DirectShow) usado para iniciar a captura;
/// <see cref="Name"/> é o nome amigável exibido na lista de seleção.
/// </summary>
public sealed record CameraDevice(string Id, string Name)
{
    public override string ToString() => Name;
}
