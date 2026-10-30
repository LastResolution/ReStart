using System.Text.Json;
using ReStart.App.Services;

namespace ReStart.Core.Tests;

public class NativeJumpContractTests
{
    private const string Owner = "Fixture.Application!App";
    private static readonly string Key = new('A', 64);
    [Theory]
    [InlineData("read", false, false)]
    [InlineData("pin", false, true)]
    [InlineData("unpin", true, false)]
    [InlineData("invoke-task", true, false)]
    [InlineData("invoke-pinned", true, false)]
    public void OnlyIdentityBasedOperationsAndExplicitPinPathsAreAccepted(string operation, bool key, bool target)
    {
        var request = new NativeJumpRequest(1, operation, Owner, key ? Key : "", target ? "owned-file.txt" : "");
        Assert.True(NativeJumpContract.ValidRequest(request));
        Assert.False(NativeJumpContract.ValidRequest(request with { Version = 2 }));
        Assert.False(NativeJumpContract.ValidRequest(request with { AppId = "" }));
        Assert.False(NativeJumpContract.ValidRequest(request with { AppId = "owner\0other" }));
        Assert.False(NativeJumpContract.ValidRequest(request with { AppId = new string('a', 257) }));
    }
    [Theory]
    [InlineData("execute", "", "program.exe")]
    [InlineData("read", "", "unexpected-file")]
    [InlineData("pin", "", "")]
    [InlineData("pin", "valid-key", "owned-file")]
    [InlineData("invoke-task", "program.exe --arguments", "")]
    [InlineData("invoke-pinned", "", "owned-file")]
    [InlineData("unpin", "", "owned-file")]
    public void RejectsCommandStringsAndUnexpectedOperationFields(string operation, string key, string target)
        => Assert.False(NativeJumpContract.ValidRequest(new(1, operation, Owner, key, target)));

    [Fact]
    public void RejectsMalformedUnboundedAndCrossApplicationReplies()
    {
        var item = new NativeJumpItem(Key, "Sample", "", false, true);
        var reply = new NativeJumpReply(true, "", Owner, new(true, true, [item], [item]));
        Assert.True(NativeJumpContract.ValidReply(reply, Owner.ToLowerInvariant()));
        Assert.False(NativeJumpContract.ValidReply(reply, "Other.App"));
        Assert.False(NativeJumpContract.ValidReply(reply with { Error = "private path or native message" }, Owner));
        Assert.False(NativeJumpContract.ValidReply(reply with { Success = false }, Owner));
        foreach (var bad in new[] { item with { Name = "" }, item with { Name = "injected\nrow" }, item with { Name = new string('x', 513) },
            item with { Target = new string('x', 32768) }, item with { Key = "command" }, item with { Key = new string('G', 64) } })
            Assert.False(NativeJumpContract.ValidReply(reply with { Snapshot = new(true, true, [bad], []) }, Owner));
        Assert.False(NativeJumpContract.ValidReply(reply with { Snapshot = new(true, true, [item, item], []) }, Owner));
        Assert.False(NativeJumpContract.ValidReply(reply with { Snapshot = new(true, true, Enumerable.Repeat(item, 65).ToArray(), []) }, Owner));
        Assert.False(NativeJumpContract.ValidReply(reply with { Snapshot = new(false, false, [item], []) }, Owner));
        Assert.False(NativeJumpContract.ValidReply(reply with { Snapshot = null! }, Owner));
    }
    [Theory]
    [InlineData("{\"Version\":1,\"Operation\":\"read\"}")]
    [InlineData("{\"Version\":1,\"Operation\":\"read\",\"AppId\":\"Sample\",\"Executable\":\"program.exe\"}")]
    public void ProtocolRejectsMissingIdentityAndExtraExecutionFields(string json)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<NativeJumpRequest>(json, NativeJumpContract.Json));
}
