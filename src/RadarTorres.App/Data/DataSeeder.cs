using System;
using RadarTorres.App.Models;
using RadarTorres.App.Repositories;
using RadarTorres.App.Services;

namespace RadarTorres.App.Data;

/// <summary>
/// Garante que exista pelo menos um usuário Administrador no primeiro uso do aplicativo —
/// sem isso ninguém conseguiria logar após uma instalação nova (as tabelas CSV começam
/// vazias). Chamado uma vez em <c>App.OnStartup</c>, depois que o contêiner de DI é montado.
/// </summary>
public static class DataSeeder
{
    public const string DefaultAdminLogin = "admin";
    public const string DefaultAdminPassword = "admin123";

    public static void EnsureDefaultAdmin(IUsuarioRepository usuarioRepository, IPasswordHasher passwordHasher)
    {
        if (usuarioRepository.GetAll().Count > 0) return;

        (string hash, string salt) = passwordHasher.Hash(DefaultAdminPassword);

        usuarioRepository.Add(new Usuario
        {
            Nome = "Administrador",
            Login = DefaultAdminLogin,
            SenhaHash = hash,
            SenhaSalt = salt,
            Perfil = PerfilUsuario.Administrador,
            Ativo = true,
            DataCriacao = DateTime.Now
        });
    }
}
