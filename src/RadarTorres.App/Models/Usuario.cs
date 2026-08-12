using System;

namespace RadarTorres.App.Models;

/// <summary>
/// Conta de usuário do aplicativo (login independente da conta do Windows). Persistida hoje
/// em <c>usuarios.csv</c> via <see cref="Repositories.IUsuarioRepository"/> — ver comentário
/// "TODO(SQL)" nesse arquivo para o plano de migração futura para um banco relacional.
/// </summary>
public class Usuario
{
    public int Id { get; set; }

    public string Nome { get; set; } = string.Empty;

    /// <summary>Login único usado para autenticação (não confundir com <see cref="Nome"/>).</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Hash PBKDF2 da senha — nunca a senha em texto puro (ver <see cref="Services.IPasswordHasher"/>).</summary>
    public string SenhaHash { get; set; } = string.Empty;

    /// <summary>Salt aleatório usado no PBKDF2, único por usuário.</summary>
    public string SenhaSalt { get; set; } = string.Empty;

    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Visualizador;

    /// <summary>Usuários inativos não conseguem autenticar, mas seus registros de auditoria são preservados.</summary>
    public bool Ativo { get; set; } = true;

    public DateTime DataCriacao { get; set; }

    public DateTime? UltimoAcesso { get; set; }
}
