using System;
using System.Security.Cryptography;

namespace RadarTorres.App.Services;

/// <summary>
/// Implementação de <see cref="IPasswordHasher"/> usando PBKDF2-HMACSHA256
/// (<see cref="Rfc2898DeriveBytes"/>, nativo do .NET — sem dependência de pacote externo),
/// com 100.000 iterações e salt aleatório de 128 bits por usuário, seguindo as recomendações
/// atuais da OWASP para hashing de senha.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public (string Hash, string Salt) Hash(string senha)
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        byte[] hashBytes = Rfc2898DeriveBytes.Pbkdf2(senha, saltBytes, Iterations, Algorithm, KeySizeBytes);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }

    public bool Verify(string senha, string hash, string salt)
    {
        byte[] saltBytes = Convert.FromBase64String(salt);
        byte[] expectedHash = Convert.FromBase64String(hash);
        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(senha, saltBytes, Iterations, Algorithm, expectedHash.Length);

        // Comparação em tempo constante — evita vazar informação por timing attack.
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
