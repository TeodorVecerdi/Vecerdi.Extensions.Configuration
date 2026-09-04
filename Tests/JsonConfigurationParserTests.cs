using System;
using NUnit.Framework;

namespace Vecerdi.Extensions.Configuration.Tests;

[TestFixture]
public sealed class JsonConfigurationParserTests {
    [Test]
    public void NestedObjects_BecomeColonSeparatedKeys() {
        var data = JsonConfigurationParser.Parse("""{ "Logging": { "LogLevel": { "Default": "Information" } } }""");

        Assert.That(data["Logging:LogLevel:Default"], Is.EqualTo("Information"));
        Assert.That(data, Has.Count.EqualTo(1));
    }

    [Test]
    public void Arrays_IndexFromZero() {
        var data = JsonConfigurationParser.Parse("""{ "Servers": [ "a", { "Host": "b" } ] }""");

        Assert.That(data["Servers:0"], Is.EqualTo("a"));
        Assert.That(data["Servers:1:Host"], Is.EqualTo("b"));
    }

    [Test]
    public void Scalars_AreStrings_AndNullStaysNull() {
        var data = JsonConfigurationParser.Parse("""{ "Count": 3, "Ratio": 1.5, "On": true, "Nothing": null }""");

        Assert.That(data["Count"], Is.EqualTo("3"));
        Assert.That(data["Ratio"], Is.EqualTo("1.5"));
        Assert.That(data["On"], Is.EqualTo("True").Or.EqualTo("true"));
        Assert.That(data.ContainsKey("Nothing"), Is.True);
        Assert.That(data["Nothing"], Is.Null);
    }

    [Test]
    public void CommentsAndTrailingCommas_AreAllowed() {
        var data = JsonConfigurationParser.Parse("""
            {
                // a comment
                "A": 1, /* another */
                "B": 2,
            }
            """);

        Assert.That(data["A"], Is.EqualTo("1"));
        Assert.That(data["B"], Is.EqualTo("2"));
    }

    [Test]
    public void Keys_AreCaseInsensitive_LastWins() {
        var data = JsonConfigurationParser.Parse("""{ "key": "first", "KEY": "second" }""");

        Assert.That(data, Has.Count.EqualTo(1));
        Assert.That(data["Key"], Is.EqualTo("second"));
    }

    [Test]
    public void ParseInto_LayersOverExistingValues() {
        var data = JsonConfigurationParser.Parse("""{ "A": "1", "B": "1" }""");
        JsonConfigurationParser.ParseInto("""{ "B": "2", "C": "2" }""", data);

        Assert.That(data["A"], Is.EqualTo("1"));
        Assert.That(data["B"], Is.EqualTo("2"));
        Assert.That(data["C"], Is.EqualTo("2"));
    }

    [Test]
    public void NonObjectRoot_Throws() {
        Assert.Throws<FormatException>(() => JsonConfigurationParser.Parse("[1, 2]"));
    }

    [Test]
    public void EmptyObject_YieldsNoKeys() {
        Assert.That(JsonConfigurationParser.Parse("{}"), Is.Empty);
    }
}
