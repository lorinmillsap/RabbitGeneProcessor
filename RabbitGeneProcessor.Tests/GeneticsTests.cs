using Xunit;
using RabbitGeneProcessor.Core;

namespace RabbitGeneProcessor.Tests;

public class GeneticsTests
{
    [Fact]
    public void Parse_SingleLetterLocus_ReturnsLocusWithUnknown()
    {
        var locus = Locus.Parse("A");
        Assert.Equal("A", locus.First.Symbol);
        Assert.Equal("_", locus.Second.Symbol);
    }

    [Fact]
    public void Parse_TwoLetterLocus_ReturnsLocusWithTwoAlleles()
    {
        var locus = Locus.Parse("Aa");
        Assert.Equal("A", locus.First.Symbol);
        Assert.Equal("a", locus.Second.Symbol);
    }

    [Fact]
    public void Parse_FourLetterLocus_ReturnsLocusWithTwoAlleles()
    {
        var locus = Locus.Parse("Enen");
        Assert.Equal("En", locus.First.Symbol);
        Assert.Equal("en", locus.Second.Symbol);
    }

    [Fact]
    public void GetOffspring_SimpleCross_ReturnsExpectedGenotypes()
    {
        // Simple cross: Aa x Aa
        var p1 = RabbitGenotype.Parse("Aa");
        var p2 = RabbitGenotype.Parse("Aa");

        var offspring = RabbitGenotype.GetOffspring(p1, p2).ToList();

        // 4 combinations: AA, Aa, aA, aa
        Assert.Equal(4, offspring.Count);
        
        var strings = offspring.Select(o => o.ToString()).ToList();
        Assert.Contains("AA", strings);
        Assert.Contains("Aa", strings);
        Assert.Contains("aA", strings);
        Assert.Contains("aa", strings);
    }

    [Fact]
    public void Parse_ComplexGenotype_ReturnsAllLoci()
    {
        var genotype = RabbitGenotype.Parse("A_,B_,C_,D_,E,enen");
        Assert.Equal(6, genotype.Loci.Count);
        Assert.Equal("A_", genotype.Loci[0].ToString());
        Assert.Equal("enen", genotype.Loci[5].ToString());
    }
}
