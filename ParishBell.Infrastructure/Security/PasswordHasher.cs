using ParishBell.Core.Interfaces;

namespace ParishBell.Infrastructure.Security;

public class PasswordHasher : IPasswordHasher
{
    // NOTE: Use BCrypt to hash the given password
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    // NOTE: Verify the password and hash using BCrypt
    public bool Verify(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);
}