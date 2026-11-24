using BizFlow.Domain.Messaging;

namespace BizFlow.UnitTests.Messaging;

public sealed class DirectMessagingTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly Guid Alice = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Bob = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Now = new(2026, 11, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_pair_key_is_the_same_whichever_way_round_the_two_people_are()
    {
        // This is what makes "open a chat with X" idempotent from either side.
        Assert.Equal(ChatConversation.Pair(Alice, Bob), ChatConversation.Pair(Bob, Alice));
        Assert.NotEqual(ChatConversation.Pair(Alice, Bob), ChatConversation.Pair(Alice, Guid.CreateVersion7()));
        Assert.Equal(65, ChatConversation.Pair(Alice, Bob).Length);
    }

    [Fact]
    public void A_direct_conversation_needs_two_different_people_and_an_opener_among_them()
    {
        var conversation = ChatConversation.CreateDirect(Tenant, Alice, Bob, Alice, Now);
        Assert.Equal(ChatConversation.Pair(Alice, Bob), conversation.PairKey);
        Assert.Equal(Alice, conversation.CreatedBy);
        Assert.Null(conversation.LastMessageAt);

        Assert.Throws<ArgumentException>(() => ChatConversation.CreateDirect(Tenant, Alice, Alice, Alice, Now));
        // A bystander cannot open a conversation between two other people.
        Assert.Throws<ArgumentException>(() =>
            ChatConversation.CreateDirect(Tenant, Alice, Bob, Guid.CreateVersion7(), Now));
        Assert.Throws<ArgumentException>(() => ChatConversation.CreateDirect(Guid.Empty, Alice, Bob, Alice, Now));
    }

    [Fact]
    public void Last_activity_only_ever_moves_forward()
    {
        var conversation = ChatConversation.CreateDirect(Tenant, Alice, Bob, Alice, Now);
        Assert.True(conversation.Touch(Now));
        Assert.Equal(Now, conversation.LastMessageAt);
        // An out-of-order write must not drag the conversation back down the list.
        Assert.False(conversation.Touch(Now.AddMinutes(-5)));
        Assert.Equal(Now, conversation.LastMessageAt);
        Assert.True(conversation.Touch(Now.AddMinutes(5)));
    }

    [Fact]
    public void A_read_marker_only_ever_moves_forward()
    {
        var participant = ChatParticipant.Create(Tenant, Guid.CreateVersion7(), Alice, Now);
        Assert.Null(participant.LastReadAt);
        Assert.True(participant.MarkRead(Now));
        Assert.False(participant.MarkRead(Now.AddMinutes(-1)));
        Assert.Equal(Now, participant.LastReadAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0000xin chào")]
    public void A_message_refuses_empty_and_control_characters(string body) =>
        Assert.Throws<ArgumentException>(() =>
            ChatMessage.Create(Tenant, Guid.CreateVersion7(), Alice, body, Now));

    [Fact]
    public void A_message_is_trimmed_bounded_and_keeps_its_line_breaks()
    {
        var message = ChatMessage.Create(Tenant, Guid.CreateVersion7(), Alice, "  dòng 1\ndòng 2  ", Now);
        Assert.Equal("dòng 1\ndòng 2", message.Body);
        Assert.Null(message.EditedAt);
        Assert.Null(message.DeletedAt);

        Assert.Throws<ArgumentException>(() => ChatMessage.Create(Tenant, Guid.CreateVersion7(), Alice,
            new string('x', ChatMessage.MaxBodyLength + 1), Now));
    }

    [Fact]
    public void Deleting_a_message_keeps_the_row_and_drops_the_words()
    {
        var message = ChatMessage.Create(Tenant, Guid.CreateVersion7(), Alice, "nhầm rồi", Now);

        Assert.True(message.Delete(Now.AddMinutes(1)));
        Assert.Equal("", message.Body);
        Assert.NotNull(message.DeletedAt);
        // Deleting twice is not an error, and a deleted message can never be edited back.
        Assert.False(message.Delete(Now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => message.Edit("quay lại", Now.AddMinutes(3)));
    }

    [Fact]
    public void Editing_records_when_it_happened_and_ignores_an_unchanged_body()
    {
        var message = ChatMessage.Create(Tenant, Guid.CreateVersion7(), Alice, "bản đầu", Now);

        Assert.False(message.Edit("bản đầu", Now.AddMinutes(1)));
        Assert.Null(message.EditedAt);

        Assert.True(message.Edit("bản sau", Now.AddMinutes(2)));
        Assert.Equal("bản sau", message.Body);
        Assert.Equal(Now.AddMinutes(2), message.EditedAt);
    }
}
