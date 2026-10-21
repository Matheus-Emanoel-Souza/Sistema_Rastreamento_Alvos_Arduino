using System;

namespace RadarTorres.App.Data;

/// <summary>
/// Descreve uma coluna de uma "tabela" CSV: como ler o valor de uma instância de
/// <typeparamref name="T"/> para gravar no arquivo, e como aplicar de volta um campo lido
/// do arquivo na instância. Ver <see cref="CsvTableStore{T}"/>.
/// </summary>
public sealed class CsvColumn<T> where T : new()
{
    public string Name { get; }
    public Func<T, string> Read { get; }
    public Action<T, string> Write { get; }

    public CsvColumn(string name, Func<T, string> read, Action<T, string> write)
    {
        Name = name;
        Read = read;
        Write = write;
    }
}
