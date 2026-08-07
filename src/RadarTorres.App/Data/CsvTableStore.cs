using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RadarTorres.App.Data;

/// <summary>
/// Armazenamento genérico de uma "tabela" em um arquivo CSV (RFC 4180 simplificado), usado
/// por todos os repositórios em <c>Repositories/</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>TODO(SQL)</b>: esta é uma solução deliberadamente simples, escolhida para a etapa atual
/// do projeto (ver <c>Documentation/MODELO_DADOS.md</c>). Quando o projeto migrar para um
/// banco relacional (SQLite/SQL Server), esta classe é substituída por um `DbContext`/ADO.NET
/// por trás da mesma interface de repositório — nenhum ViewModel ou Service precisa mudar.
/// </para>
/// <para>
/// Escapes: campos com vírgula ou aspas são colocados entre aspas (aspas internas duplicadas,
/// como no CSV padrão). Quebras de linha dentro de um campo (ex.: uma descrição de várias
/// linhas) são convertidas para a sequência literal <c>\n</c> antes de gravar e revertidas ao
/// ler — isso evita ter que lidar com campos multi-linha "de verdade" dentro de um leitor
/// linha-a-linha, o que manteria o parser simples e robusto no tamanho de dados deste projeto.
/// </para>
/// </remarks>
public sealed class CsvTableStore<T> where T : new()
{
    private readonly string _path;
    private readonly IReadOnlyList<CsvColumn<T>> _columns;
    private readonly object _lock = new();

    public CsvTableStore(string path, IReadOnlyList<CsvColumn<T>> columns)
    {
        _path = path;
        _columns = columns;
        EnsureFileWithHeader();
    }

    public List<T> ReadAll()
    {
        lock (_lock)
        {
            return ReadAllNoLock();
        }
    }

    /// <summary>Acrescenta uma linha ao final do arquivo (rápido — não reescreve a tabela inteira).</summary>
    public void Append(T item)
    {
        lock (_lock)
        {
            EnsureFileWithHeader();
            File.AppendAllText(_path, BuildLine(item) + Environment.NewLine, Encoding.UTF8);
        }
    }

    /// <summary>
    /// Reescreve a tabela inteira a partir de <paramref name="items"/>. Usado para
    /// atualizações (ex.: editar um usuário, resolver um chamado) — aceitável no volume de
    /// dados deste projeto; repositórios de auditoria pura (ações realizadas, alterações de
    /// modo, objetos detectados) não expõem nenhuma operação que chame este método.
    /// </summary>
    public void WriteAll(IEnumerable<T> items)
    {
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            using var writer = new StreamWriter(_path, append: false, Encoding.UTF8);
            writer.WriteLine(BuildHeaderLine());
            foreach (T item in items)
            {
                writer.WriteLine(BuildLine(item));
            }
        }
    }

    /// <summary>Próximo Id inteiro disponível (maior Id existente + 1, ou 1 se a tabela estiver vazia).</summary>
    public int GetNextId(Func<T, int> idSelector)
    {
        lock (_lock)
        {
            List<T> all = ReadAllNoLock();
            return all.Count == 0 ? 1 : all.Max(idSelector) + 1;
        }
    }

    private List<T> ReadAllNoLock()
    {
        var result = new List<T>();
        if (!File.Exists(_path)) return result;

        using var reader = new StreamReader(_path, Encoding.UTF8);
        string? header = reader.ReadLine(); // cabeçalho — descartado (colunas são fixas por código)
        if (header is null) return result;

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0) continue;

            List<string> fields = ParseLine(line);
            var item = new T();
            for (int i = 0; i < _columns.Count && i < fields.Count; i++)
            {
                _columns[i].Write(item, UnescapeNewlines(fields[i]));
            }
            result.Add(item);
        }
        return result;
    }

    private void EnsureFileWithHeader()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        if (!File.Exists(_path))
        {
            File.WriteAllText(_path, BuildHeaderLine() + Environment.NewLine, Encoding.UTF8);
        }
    }

    private string BuildHeaderLine() => string.Join(",", _columns.Select(c => EscapeField(c.Name)));

    private string BuildLine(T item) => string.Join(",", _columns.Select(c => EscapeField(c.Read(item))));

    private static string EscapeField(string? value)
    {
        string normalized = EscapeNewlines(value ?? string.Empty);
        bool needsQuoting = normalized.Contains(',') || normalized.Contains('"');
        return needsQuoting ? "\"" + normalized.Replace("\"", "\"\"") + "\"" : normalized;
    }

    private static string EscapeNewlines(string value) =>
        value.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n");

    private static string UnescapeNewlines(string value) => value.Replace("\\n", "\n");

    /// <summary>Parser de uma linha CSV com suporte a campos entre aspas (com vírgula/aspas internas).</summary>
    private static List<string> ParseLine(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }

        fields.Add(sb.ToString());
        return fields;
    }
}
