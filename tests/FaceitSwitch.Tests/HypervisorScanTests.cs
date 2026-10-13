using System.Globalization;
using System.Text;
using FaceitSwitch.Core.Models;
using FaceitSwitch.Core.Services;

namespace FaceitSwitch.Tests;

public sealed class HypervisorScanTests
{
    [Theory]
    [InlineData("hypervisorlaunchtype Auto\r\n", SettingState.Enabled)]
    [InlineData("hypervisorlaunchtype Off\n", SettingState.Disabled)]
    [InlineData("\t hypervisorlaunchtype\t\tAUTO  \r\n", SettingState.Enabled)]
    [InlineData("  HYPERVISORLAUNCHTYPE    auto   \n", SettingState.Enabled)]
    [InlineData("hypervisorlaunchtype\tOfF\r\n", SettingState.Disabled)]
    [InlineData("isolatedcontext Yes\r\nhypervisorlaunchtype Auto\r\nhypervisoriommupolicy Enable\r\n", SettingState.Enabled)]
    [InlineData("isolatedcontext Yes\nhypervisoriommupolicy Enable\nhypervisorlaunchtype off\n", SettingState.Disabled)]
    [InlineData("isolatedcontext Yes\nhypervisoriommupolicy Enable\n", SettingState.Enabled)]
    [InlineData("hypervisorlaunchtype unexpected\n", SettingState.Unknown)]
    [InlineData("hypervisorlaunchtype\nisolatedcontext Yes\n", SettingState.Unknown)]
    [InlineData("hypervisorlaunchtype Auto extra\n", SettingState.Unknown)]
    [InlineData("hypervisorlaunchtype Auto\nhypervisorlaunchtype Off\n", SettingState.Unknown)]
    [InlineData("hypervisorlaunchtypeExtra Off\nisolatedcontext Yes\n", SettingState.Enabled)]
    [InlineData("\uFEFFhypervisorlaunchtype Auto\r\n", SettingState.Enabled)]
    [InlineData("\uFEFF \r\n\t", SettingState.Error)]
    [InlineData("", SettingState.Error)]
    [InlineData(" \r\n\t", SettingState.Error)]
    public void ScannerParsesLinesAndExactKeys(string output, SettingState expected)
        => Assert.Equal(expected, HypervisorService.ParseConfiguration(output).State);

    [Theory]
    [InlineData(850)]
    [InlineData(437)]
    [InlineData(65001)]
    public void DecodesNativeOemAndUtf8Output(int codePage)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        const string output = "Windowsin käynnistyslataaja\r\nisolatedcontext Yes\r\nhypervisorlaunchtype Auto\r\n";
        var bytes = Encoding.GetEncoding(codePage).GetBytes(output);
        var decoded = ProcessOutputDecoder.Decode(bytes, codePage);
        Assert.Equal(output, decoded);
        Assert.Equal(SettingState.Enabled, HypervisorService.ParseConfiguration(decoded).State);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DecodesUtf16WithAndWithoutBom(bool bigEndian, bool withBom)
    {
        const string output = "Windowsin käynnistyslataaja\r\nhypervisorlaunchtype Off\r\nisolatedcontext Yes\r\n";
        var encoding = bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;
        var bytes = withBom ? encoding.GetPreamble().Concat(encoding.GetBytes(output)).ToArray() : encoding.GetBytes(output);
        var decoded = ProcessOutputDecoder.Decode(bytes, 850);
        Assert.Equal(output, decoded);
        Assert.Equal(SettingState.Disabled, HypervisorService.ParseConfiguration(decoded).State);
    }

    [Fact]
    public void DecodesUtf8BomAndPreservesDiagnosticText()
    {
        const string output = "Käyttö estetty — hypervisorlaunchtype Auto\r\n";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(output)).ToArray();
        Assert.Equal(output, ProcessOutputDecoder.Decode(bytes, 850));
    }

    [Fact]
    public void EmptyProcessOutputRemainsAnError()
        => Assert.Equal(SettingState.Error, HypervisorService.ParseConfiguration(ProcessOutputDecoder.Decode([], 850)).State);

    [Fact]
    public void ReproducesUnsupportedCultureOemCodePageFromVersion101()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-FI");
            // In v1.0.1 this lookup happened before Process.Start, failing the scan even with valid BCD.
            Assert.Equal(1, CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
            Assert.Throws<ArgumentException>(() => Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage));
            var decoded = ProcessOutputDecoder.Decode(Encoding.ASCII.GetBytes("hypervisorlaunchtype Auto\r\n"), 850);
            Assert.Equal(SettingState.Enabled, HypervisorService.ParseConfiguration(decoded).State);
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    [Fact]
    public async Task ScanOfValidEntryIgnoresOtherBcdElementsAndNeverWrites()
    {
        var process = new FakeProcess
        {
            ReadOutput = "isolatedcontext Yes\r\nhypervisorlaunchtype Auto\r\nhypervisoriommupolicy Enable\r\n"
        };
        var controller = new HypervisorService(process, new FakeLog());
        Assert.Equal(SettingState.Enabled, (await controller.ReadStateAsync()).State);
        var call = Assert.Single(process.Calls);
        Assert.Equal("bcdedit.exe", call.Executable);
        Assert.Equal(new[] { "/enum", "{current}" }, call.Arguments);
    }
}
