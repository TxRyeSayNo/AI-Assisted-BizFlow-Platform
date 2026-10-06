using BizFlow.Infrastructure.Authentication;

namespace BizFlow.IntegrationTests;

public sealed class RefreshTokenGeneratorTests
{
    [Fact]
    public void Tokens_have_512_random_bits_and_only_sha256_hash_is_persisted()
    {
        var generator = new RefreshTokenGenerator();
        var first = generator.Generate();
        var second = generator.Generate();
        Assert.Equal(128, first.Secret.Length);
        Assert.Equal(64, first.Hash.Length);
        Assert.Equal(generator.Hash(first.Secret), first.Hash);
        Assert.NotEqual(first.Secret, second.Secret);
        Assert.NotEqual(first.Hash, second.Hash);
        Assert.DoesNotContain(first.Secret, first.ToString());
    }
}
