namespace Sekretujo.Core.Services;

public sealed class InvalidTotpSecretException : Exception
{
    public InvalidTotpSecretException(string message, Exception inner) : base(message, inner) { }
}
