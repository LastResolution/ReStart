using System.Text.Json;
using ReStart.Core;
using ReStart.TileHost;

namespace ReStart.Core.Tests;

[Collection("JavaScript runtime")]
public sealed class ScriptTileTests
{
    private static ScriptTileFrame Render(string source, object? context = null) => JsTileRunner.Render(new(source, JsonSerializer.SerializeToElement(context ?? new { now = "2026-10-18T09:41:00+09:00", state = new { }, settings = new { }, locale = "en-US", size = "wide" })));

    [Fact]
    public void RendersPureJavaScriptWithContextAndState()
    {
        var frame = Render("function render(ctx) { return {text: ctx.locale, detail: String(ctx.state.count + 1), progress: .5, state: {count:ctx.state.count+1}}; }", new { locale = "ja-JP", state = new { count = 2 } });
        Assert.Equal("ja-JP", frame.Text); Assert.Equal("3", frame.Detail);
        Assert.Equal(3, frame.State.GetProperty("count").GetInt32());
    }
    [Theory]
    [InlineData("function render(){ while(true) {} }")]
    [InlineData("function render(){ throw new Error('broken'); }")]
    [InlineData("function render(){ return {background:'url(file:///secret)'}; }")]
    [InlineData("function render(){ return {progress:2}; }")]
    [InlineData("function render(){ return {text:'x'.repeat(257)}; }")]
    [InlineData("function render(){ return eval('({text: 1})'); }")]
    [InlineData("function render(){ return System.IO.File.ReadAllText('secret'); }")]
    [InlineData("function render(){ return require('fs'); }")]
    [InlineData("function render(){ return new Function('return {text: 1}')(); }")]
    [InlineData("function render(){ return ({}).constructor.constructor('return process')(); }")]
    [InlineData("function render(){ return importNamespace('System').Diagnostics.Process.Start('blocked'); }")]
    [InlineData("function render(){ function f(){return f()+1;} return f(); }")]
    public void RejectsInvalidFramesAndStopsRunawayScripts(string source) => Assert.ThrowsAny<Exception>(() => Render(source));

    [Fact]
    public void UsesFreshRealmForEveryRender()
    {
        Render("function render(){ Object.prototype.leak = true; return {}; }");
        Assert.Equal("undefined", Render("function render(){ return {text:typeof ({}).leak}; }").Text);
        Assert.Equal("undefined,undefined,undefined,undefined", Render("function render(){ return {text:[typeof fetch,typeof process,typeof importNamespace,typeof document].join(',')}; }").Text);
        Assert.Equal("undefined,undefined,undefined,undefined,undefined", Render("function render(){ return {text:[typeof require,typeof Buffer,typeof System,typeof ActiveXObject,typeof __contextJson].join(',')}; }").Text);
    }

    [Fact]
    public async Task BoundsWorkerProtocolBeforeParsing()
    {
        Assert.Equal("hello", await ScriptTileProtocol.ReadLineAsync(new StringReader("hello\n"), 5));
        await Assert.ThrowsAsync<InvalidDataException>(() => ScriptTileProtocol.ReadLineAsync(new StringReader("123456"), 5));
    }
    [Fact]
    public void ValidatesManifestRefreshAndSettings()
    {
        new ScriptTileManifest().Validate();
        Assert.Throws<InvalidDataException>(() => new ScriptTileManifest { RefreshSeconds = 0 }.Validate());
        Assert.Throws<InvalidDataException>(() => new ScriptTileManifest { Settings = JsonSerializer.SerializeToElement("text") }.Validate());
    }
}
