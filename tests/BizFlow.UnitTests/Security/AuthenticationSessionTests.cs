using BizFlow.Domain.Authentication;

namespace BizFlow.UnitTests.Security;

public sealed class AuthenticationSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 29, 0, 0, 0, TimeSpan.Zero);
    private static readonly string Hash = new('a', 64);

    [Fact]
    public void Rotation_preserves_family_and_absolute_expiry_and_consumes_original()
    {
        var session = AuthenticationSession.Create(Guid.NewGuid(), Hash, Now, Now.AddDays(14));
        var next = session.Rotate(new string('b', 64), Now.AddHours(1));
        Assert.Equal(session.UserId, next.UserId);
        Assert.Equal(session.FamilyId, next.FamilyId);
        Assert.Equal(session.ExpiresAt, next.ExpiresAt);
        Assert.Equal(next.Id, session.ReplacedById);
        Assert.Equal(Now.AddHours(1), session.RevokedAt);
        Assert.False(session.CanRefresh(Now.AddHours(1)));
        Assert.True(next.CanRefresh(Now.AddHours(1)));
    }

    [Fact]
    public void Token_cannot_rotate_twice()
    {
        var session = AuthenticationSession.Create(Guid.NewGuid(), Hash, Now, Now.AddDays(14));
        session.Rotate(new string('b', 64), Now.AddHours(1));
        Assert.Throws<InvalidOperationException>(() => session.Rotate(new string('c', 64), Now.AddHours(2)));
    }

    [Fact]
    public void Expired_revoked_and_not_yet_valid_sessions_cannot_refresh()
    {
        var session = AuthenticationSession.Create(Guid.NewGuid(), Hash, Now, Now.AddDays(14));
        Assert.False(session.CanRefresh(Now.AddMinutes(-1)));
        Assert.False(session.CanRefresh(Now.AddDays(14)));
        session.Revoke(Now.AddMinutes(1));
        Assert.False(session.CanRefresh(Now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => session.Rotate(new string('b', 64), Now.AddMinutes(2)));
    }

    [Fact]
    public void Hashes_are_canonical_and_differing_case_does_not_count_as_rotation()
    {
        var session = AuthenticationSession.Create(Guid.NewGuid(), Hash.ToUpperInvariant(), Now, Now.AddDays(1));
        Assert.Equal(Hash, session.TokenHash);
        Assert.Throws<ArgumentException>(() => session.Rotate(Hash.ToUpperInvariant(), Now.AddMinutes(1)));
        Assert.True(session.CanRefresh(Now.AddMinutes(1)));
    }

    [Fact]
    public void Invalid_hash_identity_and_expiry_are_rejected()
    {
        Assert.Throws<ArgumentException>(() => AuthenticationSession.Create(Guid.Empty, Hash, Now, Now.AddDays(1)));
        Assert.Throws<ArgumentException>(() => AuthenticationSession.Create(Guid.NewGuid(), "plaintext-token", Now, Now.AddDays(1)));
        Assert.Throws<ArgumentException>(() => AuthenticationSession.Create(Guid.NewGuid(), Hash, Now, Now));
    }
}
