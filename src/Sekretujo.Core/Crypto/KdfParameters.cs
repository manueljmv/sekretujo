namespace Sekretujo.Core.Crypto;

public sealed record KdfParameters(int MemoryKiB, int Iterations, int Parallelism)
{
    public static KdfParameters Default { get; } = new(65536, 3, 4);
}
