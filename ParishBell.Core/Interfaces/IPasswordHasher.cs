namespace ParishBell.Core.Interfaces;

public interface IPasswordHasher
{
    // NOTE: Hashes a given password using BCrypt
    string Hash(string password);

    // NOTE: Verifies a given password with a given hash
    bool Verify(string password, string hash);
}