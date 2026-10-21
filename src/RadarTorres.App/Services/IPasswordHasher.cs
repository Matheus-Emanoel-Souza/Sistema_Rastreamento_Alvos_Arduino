namespace RadarTorres.App.Services;

/// <summary>
/// Hash seguro de senhas (PBKDF2/SHA-256 — <see cref="PasswordHasher"/>). Senha em texto puro
/// nunca é persistida; apenas hash + salt (Requisito 7/11).
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Gera um novo salt aleatório e retorna (hash, salt) prontos para persistir.</summary>
    (string Hash, string Salt) Hash(string senha);

    /// <summary>Confere se <paramref name="senha"/> corresponde ao par (hash, salt) armazenado.</summary>
    bool Verify(string senha, string hash, string salt);
}
