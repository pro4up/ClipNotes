using ClipNotes.Services;
using Xunit;

namespace ClipNotes.Tests;

public class UpdateServiceTests
{
    // ── IsVersionNewer ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("1.1.0", "1.0.0", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("2.0.0", "1.9.9", true)]
    [InlineData("10.0.0", "9.99.99", true)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("0.9.9", "1.0.0", false)]
    [InlineData("1.0.0", "1.1.0", false)]
    [InlineData("1.0.0", "2.0.0", false)]
    public void IsVersionNewer_ReturnsExpected(string latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsVersionNewer(latest, current));
    }

    [Fact]
    public void IsVersionNewer_InvalidVersion_FallsBackToLexicographic()
    {
        // "beta2" > "beta1" lexicographically
        Assert.True(UpdateService.IsVersionNewer("beta2", "beta1"));
        Assert.False(UpdateService.IsVersionNewer("alpha", "beta"));
    }

    // ── ParseBundleHash ─────────────────────────────────────────────────────

    [Fact]
    public void ParseBundleHash_ValidEntry_ReturnsUppercaseHash()
    {
        var text = "abc123def456  ClipNotes-bundle.zip\nother789  other.zip";
        Assert.Equal("ABC123DEF456", UpdateService.ParseBundleHash(text));
    }

    [Fact]
    public void ParseBundleHash_CaseInsensitiveFilename_Matches()
    {
        var text = "abcdef  CLIPNOTES-BUNDLE.ZIP";
        Assert.Equal("ABCDEF", UpdateService.ParseBundleHash(text));
    }

    [Fact]
    public void ParseBundleHash_TabSeparator_Parses()
    {
        var text = "deadbeef\tClipNotes-bundle.zip";
        Assert.Equal("DEADBEEF", UpdateService.ParseBundleHash(text));
    }

    [Fact]
    public void ParseBundleHash_NoMatchingEntry_ReturnsNull()
    {
        var text = "abc123  other-file.zip\nxyz789  something.txt";
        Assert.Null(UpdateService.ParseBundleHash(text));
    }

    [Fact]
    public void ParseBundleHash_EmptyText_ReturnsNull()
    {
        Assert.Null(UpdateService.ParseBundleHash(""));
    }

    [Fact]
    public void ParseBundleHash_WindowsLineEndings_Parses()
    {
        var text = "aabbcc  ClipNotes-bundle.zip\r\nother  other.zip";
        Assert.Equal("AABBCC", UpdateService.ParseBundleHash(text));
    }

    [Fact]
    public void ParseBundleHash_MalformedLine_SkipsAndReturnsNull()
    {
        // Line with only one token — should be skipped without throwing
        var text = "singletoken\nClipNotes-bundle.zip";
        Assert.Null(UpdateService.ParseBundleHash(text));
    }

    // ── EscapePs ────────────────────────────────────────────────────────────

    [Fact]
    public void EscapePs_NormalPath_Unchanged()
    {
        const string path = @"C:\Users\John\AppData\Local\Temp\script.ps1";
        Assert.Equal(path, UpdateService.EscapePs(path));
    }

    [Fact]
    public void EscapePs_SingleQuote_Doubled()
    {
        Assert.Equal("C:\\Users\\O''Brien\\file.ps1",
            UpdateService.EscapePs("C:\\Users\\O'Brien\\file.ps1"));
    }

    [Fact]
    public void EscapePs_MultipleSingleQuotes_AllDoubled()
    {
        Assert.Equal("it''s a ''path''", UpdateService.EscapePs("it's a 'path'"));
    }

    [Fact]
    public void EscapePs_PathWithSpaces_Unchanged()
    {
        const string path = @"C:\Users\John Doe\AppData\Local\Temp\updater.ps1";
        Assert.Equal(path, UpdateService.EscapePs(path));
    }
}
