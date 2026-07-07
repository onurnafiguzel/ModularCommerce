namespace ModularCommerce.Identity.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string passwordHash, string providedPassword);
    string DummyHash { get; }
}
