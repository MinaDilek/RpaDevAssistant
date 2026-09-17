using System.IO.Compression;
using System.Xml.Linq;
using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Config;
using RpaDevAssistant.Core.Scanning;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class UiPathConfigAnalysisTests
{
    [Fact]
    public void Analyze_DetectsUnusedAndMissingConfigKeys()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("ExistingKey", "ok"), ("UnusedKey", "old"));
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <Assign DisplayName="Use Config" To="[value]" Value="[Config(&quot;ExistingKey&quot;)]" />
              <Assign DisplayName="Missing Config" To="[value]" Value="[Config(&quot;MissingKey&quot;)]" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath);

        Assert.True(result.ConfigFound);
        Assert.Contains(result.UnusedKeys, key => key.Entry.Key == "UnusedKey");
        Assert.Contains(result.MissingKeys, key => key.Key == "MissingKey");
        Assert.DoesNotContain(result.MissingKeys, key => key.Key == "ExistingKey");
    }

    [Fact]
    public void Analyze_ReportsUnreferencedSelectorConfigEntryAsUnused()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(
            ("UsedSelector", "<webctrl id='used' tag='BUTTON' />"),
            ("UnusedSelector", "<webctrl id='unused' tag='BUTTON' />"));
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <Assign DisplayName="Load selector" To="[selector]" Value="[Config(&quot;UsedSelector&quot;)]" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath);

        Assert.Contains(result.UnusedKeys, item => item.Entry.Key == "UnusedSelector");
        Assert.DoesNotContain(result.UnusedKeys, item => item.Entry.Key == "UsedSelector");
    }

    [Fact]
    public void Analyze_UsesManuallySelectedConfigWorkbook()
    {
        using var project = ConfigProjectFixture.Create();
        var configPath = Path.Combine(project.RootPath, "Settings", "RobotConfig.xlsx");
        project.WriteConfigTo(configPath, ("CustomKey", "ok"));
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <Assign DisplayName="Use Config" To="[value]" Value="[Config(&quot;CustomKey&quot;)]" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath, configPath);

        Assert.True(result.ConfigFound);
        Assert.Equal(Path.GetFullPath(configPath), result.ConfigPath);
        Assert.Contains(result.Entries, entry => entry.Key == "CustomKey");
        Assert.DoesNotContain(result.MissingKeys, key => key.Key == "CustomKey");
    }

    [Fact]
    public void Analyze_ManualConfigOverridesAutoDiscoveredConfig()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("AutoKey", "auto"));
        var manualConfigPath = Path.Combine(project.RootPath, "Data", "Config_Recreated_From_Screenshots.xlsx");
        project.WriteConfigTo(manualConfigPath, ("ManualKey", "manual"));
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <Assign DisplayName="Use Manual Config" To="[value]" Value="[Config(&quot;ManualKey&quot;)]" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath, manualConfigPath);

        Assert.True(result.ConfigFound);
        Assert.Equal(Path.GetFullPath(manualConfigPath), result.ConfigPath);
        Assert.Contains(result.Entries, entry => entry.Key == "ManualKey");
        Assert.DoesNotContain(result.Entries, entry => entry.Key == "AutoKey");
        Assert.DoesNotContain(result.MissingKeys, key => key.Key == "ManualKey");
    }

    [Fact]
    public void Analyze_InvalidManualConfigDoesNotFallBackToAutoConfig()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("AutoKey", "auto"));
        var invalidConfigPath = Path.Combine(project.RootPath, "Data", "Config_Recreated_From_Screenshots.xlsx");
        File.WriteAllText(invalidConfigPath, "not an xlsx workbook");
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <Assign DisplayName="Use Auto Config" To="[value]" Value="[Config(&quot;AutoKey&quot;)]" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath, invalidConfigPath);

        Assert.False(result.ConfigFound);
        Assert.Null(result.ConfigPath);
        Assert.Empty(result.Entries);
        Assert.Contains(result.Messages, message => message.Contains("selected Config file could not be read", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Messages, message => message.Contains("Config.xlsx was not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_AcceptsSelectedConfigOutsideProject()
    {
        using var project = ConfigProjectFixture.Create();
        var outsidePath = Path.Combine(Path.GetTempPath(), $"outside-config-{Guid.NewGuid():N}.xlsx");
        try
        {
            project.WriteConfigTo(outsidePath, ("CustomKey", "ok"));

            var result = CreateService().Analyze(project.RootPath, outsidePath);

            Assert.True(result.ConfigFound);
            Assert.Equal(Path.GetFullPath(outsidePath), result.ConfigPath);
            Assert.Contains(result.Entries, entry => entry.Key == "CustomKey");
        }
        finally
        {
            if (File.Exists(outsidePath))
            {
                File.Delete(outsidePath);
            }
        }
    }

    [Fact]
    public void Analyze_DetectsConservativeHardCodedCandidates()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("ExistingKey", "ok"));
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:HTTPClient DisplayName="HTTP Request" EndPoint="&quot;https://api.contoso.com/v1/customers&quot;" TimeoutMS="120000" />
              <ui:SendMail DisplayName="Send Mail" To="&quot;ops@contoso.com&quot;" Subject="&quot;Hello&quot;" />
              <ui:Click DisplayName="Click" Selector="&lt;webctrl tag='BUTTON' idx='2' /&gt;" />
              <Assign DisplayName="Secret" To="[password]" Value="&quot;FakeSecret123!&quot;" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath);

        Assert.Contains(result.HardCodedCandidates, candidate => candidate.Type == UiPathHardCodedConfigCandidateType.ApiEndpoint);
        Assert.Contains(result.HardCodedCandidates, candidate => candidate.Type == UiPathHardCodedConfigCandidateType.Email);
        Assert.Contains(result.HardCodedCandidates, candidate => candidate.Type == UiPathHardCodedConfigCandidateType.TimeoutOrRetry);
        Assert.Contains(result.HardCodedCandidates, candidate => candidate.IsSensitive && candidate.DisplayValue == "[REDACTED]" && !candidate.CanAddToConfig);
        Assert.DoesNotContain(result.HardCodedCandidates, candidate => candidate.DisplayValue.Contains("webctrl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_AggregatesRepeatedHardCodedCandidates()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("ExistingKey", "ok"));
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:HTTPClient DisplayName="HTTP Request A" EndPoint="&quot;https://api.contoso.com/v1/customers&quot;" />
              <ui:HTTPClient DisplayName="HTTP Request B" EndPoint="&quot;https://api.contoso.com/v1/customers&quot;" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath);

        var candidate = Assert.Single(result.HardCodedCandidates, item => item.DisplayValue == "https://api.contoso.com/v1/customers");
        Assert.Equal(2, candidate.OccurrenceCount);
        Assert.Equal(2, candidate.Occurrences.Count);
    }

    [Fact]
    public void Analyze_EvaluatesStaticSelectorLiteralValues()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("ExistingKey", "ok"));
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click DisplayName="Click" Selector="&lt;webctrl id='https://portal.contoso.com/login' tag='BUTTON' /&gt;" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath);

        var candidate = Assert.Single(result.HardCodedCandidates, item => item.DisplayValue == "https://portal.contoso.com/login");
        Assert.Equal(UiPathHardCodedConfigCandidateType.StaticSelector, candidate.Type);
        Assert.Equal("Selector", candidate.Occurrences.Single().PropertyName);
        Assert.False(candidate.CanAddToConfig);
        Assert.DoesNotContain(result.HardCodedCandidates, item => item.DisplayValue.Contains("webctrl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_ExcludesDynamicSelectorExpressions()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("ExistingKey", "ok"));
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click DisplayName="Click" Selector="&quot;&lt;webctrl id='https://portal.contoso.com/&quot; + customerName + &quot;' tag='BUTTON' /&gt;&quot;" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath);

        Assert.DoesNotContain(result.HardCodedCandidates, item => item.DisplayValue.Contains("portal.contoso.com", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_ExcludesSelectorContainingVariablePlaceholder()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("ExistingKey", "ok"));
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click DisplayName="Click" Selector="&lt;webctrl id='[customerName]' href='https://portal.contoso.com/customer' tag='BUTTON' /&gt;" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath);

        Assert.DoesNotContain(result.HardCodedCandidates, item => item.DisplayValue == "https://portal.contoso.com/customer");
    }

    [Fact]
    public void Analyze_StaticSelectorCandidateKeepsEvidenceAndReviewOnlyStatus()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click DisplayName="Login Button" Selector="&lt;webctrl id='btnLogin' tag='BUTTON' /&gt;" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath);

        var candidate = Assert.Single(result.HardCodedCandidates, item => item.DisplayValue == "btnLogin");
        Assert.Equal(UiPathHardCodedConfigCandidateType.StaticSelector, candidate.Type);
        Assert.False(candidate.CanAddToConfig);
        var occurrence = Assert.Single(candidate.Occurrences);
        Assert.Equal("Main.xaml", occurrence.WorkflowPath);
        Assert.Equal("Click", occurrence.ActivityName);
        Assert.Equal("Login Button", occurrence.ActivityDisplayName);
        Assert.Equal("Selector", occurrence.PropertyName);
    }

    [Fact]
    public void Analyze_SelectorContainingOnlyFixedLiteralsIsNotGloballyExcluded()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("ExistingKey", "ok"));
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click DisplayName="Click" Selector="&lt;webctrl href='C:\Reports\output.xlsx' tag='A' aaname='Download' /&gt;" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath);

        Assert.Contains(result.HardCodedCandidates, item =>
            item.DisplayValue == @"C:\Reports\output.xlsx" &&
            item.Type == UiPathHardCodedConfigCandidateType.StaticSelector);
        Assert.DoesNotContain(result.HardCodedCandidates, item => item.DisplayValue == "A");
    }

    [Fact]
    public void Analyze_EvaluatesNestedStaticClickSelectorInArgument()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("ExistingKey", "ok"));
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:Click DisplayName="Login Button">
                <ui:Click.Selector>
                  <InArgument x:TypeArguments="x:String">
                    <![CDATA[<webctrl href='https://portal.contoso.com/login' id='btnLogin' tag='BUTTON' />]]>
                  </InArgument>
                </ui:Click.Selector>
              </ui:Click>
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath);

        Assert.Contains(result.HardCodedCandidates, item => item.DisplayValue == "https://portal.contoso.com/login");
        Assert.DoesNotContain(result.HardCodedCandidates, item => item.DisplayValue.Contains("webctrl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Analyze_ProducesHardCodedCandidatesWhenConfigWorkbookIsMissing()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:HTTPClient DisplayName="HTTP Request" EndPoint="&quot;https://api.contoso.com/v1/customers&quot;" />
              <ui:Click DisplayName="Login Button" Selector="&lt;webctrl id='btnLogin' tag='BUTTON' /&gt;" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath);

        Assert.False(result.ConfigFound);
        Assert.Contains(result.HardCodedCandidates, item => item.Type == UiPathHardCodedConfigCandidateType.ApiEndpoint);
        Assert.Contains(result.HardCodedCandidates, item => item.Type == UiPathHardCodedConfigCandidateType.StaticSelector);
    }

    [Fact]
    public void Analyze_ExcludesExplicitlyNamedTestWorkflowsFromHardCodedCandidates()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteWorkflow("Business/Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:HTTPClient DisplayName="Production Request" EndPoint="&quot;https://api.contoso.com/production&quot;" />
            </Sequence.Activities>
          </Sequence>
        """));
        project.WriteWorkflow("Tests/test.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:HTTPClient DisplayName="Test Request" EndPoint="&quot;https://api.contoso.com/test&quot;" />
            </Sequence.Activities>
          </Sequence>
        """));
        project.WriteWorkflow("Tests/Test1.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:HTTPClient DisplayName="Test Request 1" EndPoint="&quot;https://api.contoso.com/test1&quot;" />
            </Sequence.Activities>
          </Sequence>
        """));
        project.WriteWorkflow("Tests/test_2.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <ui:HTTPClient DisplayName="Test Request 2" EndPoint="&quot;https://api.contoso.com/test2&quot;" />
            </Sequence.Activities>
          </Sequence>
        """));

        var result = CreateService().Analyze(project.RootPath);

        Assert.Contains(result.HardCodedCandidates, item => item.DisplayValue == "https://api.contoso.com/production");
        Assert.DoesNotContain(result.HardCodedCandidates, item => item.DisplayValue == "https://api.contoso.com/test");
        Assert.DoesNotContain(result.HardCodedCandidates, item => item.DisplayValue == "https://api.contoso.com/test1");
        Assert.DoesNotContain(result.HardCodedCandidates, item => item.DisplayValue == "https://api.contoso.com/test2");
    }

    [Fact]
    public void Preview_PreventsAddingExistingConfigKeys()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("ExistingKey", "ok"));
        project.WriteWorkflow("Main.xaml", Workflow("<Sequence />"));

        var preview = CreateService().PreviewChanges(new UiPathConfigChangePreviewRequest
        {
            ProjectPath = project.RootPath,
            Additions = [new UiPathConfigAdditionRequest { Key = "ExistingKey", Value = "new" }]
        });

        Assert.False(preview.IsValid);
        Assert.Contains(preview.ValidationMessages, message => message.Contains("existing Config key", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(preview.Changes, change => change.ChangeType == UiPathConfigPreviewChangeType.Add);
    }

    [Fact]
    public void Preview_AllowsUnrelatedAdditionWhenSourceContainsDuplicateKeys()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("DuplicateKey", "first"), ("DuplicateKey", "second"));
        project.WriteWorkflow("Main.xaml", Workflow("<Sequence />"));

        var preview = CreateService().PreviewChanges(new UiPathConfigChangePreviewRequest
        {
            ProjectPath = project.RootPath,
            Additions = [new UiPathConfigAdditionRequest { Key = "NewKey", Value = "new" }]
        });

        Assert.True(preview.IsValid);
        Assert.Contains(preview.Changes, change => change.ChangeType == UiPathConfigPreviewChangeType.Add && change.Key == "NewKey");
    }

    [Fact]
    public void Preview_BlocksRemovingDuplicateKeyBecauseTargetRowIsAmbiguous()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("DuplicateKey", "first"), ("DuplicateKey", "second"));
        project.WriteWorkflow("Main.xaml", Workflow("<Sequence />"));

        var preview = CreateService().PreviewChanges(new UiPathConfigChangePreviewRequest
        {
            ProjectPath = project.RootPath,
            RemoveKeys = ["DuplicateKey"]
        });

        Assert.False(preview.IsValid);
        Assert.Contains(preview.ValidationMessages, message => message.Contains("duplicate Config key", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(preview.Changes, change => change.ChangeType == UiPathConfigPreviewChangeType.Remove);
    }

    [Fact]
    public void Preview_DoesNotPreselectRemovalAndValidatesSelectedChanges()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("UnusedKey", "old"));
        project.WriteWorkflow("Main.xaml", Workflow("""
          <Sequence>
            <Sequence.Activities>
              <Assign DisplayName="Missing Config" To="[value]" Value="[Config(&quot;MissingKey&quot;)]" />
            </Sequence.Activities>
          </Sequence>
        """));
        var service = CreateService();

        var preview = service.PreviewChanges(new UiPathConfigChangePreviewRequest
        {
            ProjectPath = project.RootPath,
            RemoveKeys = ["UnusedKey"],
            Additions = [new UiPathConfigAdditionRequest { Key = "MissingKey", Value = "" }]
        });

        Assert.True(preview.IsValid);
        Assert.Contains(preview.Changes, change => change.ChangeType == UiPathConfigPreviewChangeType.Remove && change.Key == "UnusedKey");
        Assert.Contains(preview.Changes, change => change.ChangeType == UiPathConfigPreviewChangeType.Add && change.Key == "MissingKey");
    }

    [Fact]
    public void Generate_WritesNewWorkbookWithoutChangingOriginal()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("KeepKey", "keep"), ("RemoveKey", "remove"));
        project.WriteWorkflow("Main.xaml", Workflow("<Sequence />"));
        var outputPath = Path.Combine(project.RootPath, "Data", "Config.Generated.xlsx");

        var result = CreateService().Generate(new UiPathConfigGenerateRequest
        {
            ProjectPath = project.RootPath,
            OutputPath = outputPath,
            RemoveKeys = ["RemoveKey"],
            Additions = [new UiPathConfigAdditionRequest { Key = "NewKey", Value = "new" }]
        });

        Assert.True(result.Success);
        Assert.True(File.Exists(outputPath));

        var service = CreateService();
        var original = service.Analyze(project.RootPath);
        Assert.Contains(original.Entries, entry => entry.Key == "RemoveKey");

        var generated = ReadWorkbookKeys(outputPath);
        Assert.Contains("KeepKey", generated);
        Assert.Contains("NewKey", generated);
        Assert.DoesNotContain("RemoveKey", generated);
    }

    [Fact]
    public void Generate_AllowsOutputOutsideProject()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteConfig(("KeepKey", "keep"));
        project.WriteWorkflow("Main.xaml", Workflow("<Sequence />"));
        var outputPath = Path.Combine(Path.GetTempPath(), $"rpadev-config-output-{Guid.NewGuid():N}.xlsx");

        try
        {
            var result = CreateService().Generate(new UiPathConfigGenerateRequest
            {
                ProjectPath = project.RootPath,
                OutputPath = outputPath,
                Additions = [new UiPathConfigAdditionRequest { Key = "NewKey", Value = "new" }]
            });

            Assert.True(result.Success);
            Assert.Equal(Path.GetFullPath(outputPath), result.OutputPath);
            Assert.Contains("NewKey", ReadWorkbookKeys(outputPath));
        }
        finally
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }
        }
    }

    [Fact]
    public void Generate_PreservesAdvancedWorkbookStructuresAndExtendsAddedRowRanges()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteAdvancedConfig(("KeepKey", "keep"), ("SecondKey", "second"));
        project.WriteWorkflow("Main.xaml", Workflow("<Sequence />"));
        var sourcePath = Path.Combine(project.RootPath, "Data", "Config.xlsx");
        var outputPath = Path.Combine(project.RootPath, "Data", "Config.Generated.xlsx");

        var result = CreateService().Generate(new UiPathConfigGenerateRequest
        {
            ProjectPath = project.RootPath,
            OutputPath = outputPath,
            Additions = [new UiPathConfigAdditionRequest { Key = "NewKey", Value = "new" }]
        });

        Assert.True(result.Success);
        Assert.Equal(ReadArchiveEntry(sourcePath, "xl/styles.xml"), ReadArchiveEntry(outputPath, "xl/styles.xml"));
        Assert.Equal(ReadArchiveEntry(sourcePath, "docProps/core.xml"), ReadArchiveEntry(outputPath, "docProps/core.xml"));

        var sheet = ReadArchiveXml(outputPath, "xl/worksheets/sheet1.xml");
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        Assert.Equal("A1:F4", (string?)sheet.Root?.Element(main + "dimension")?.Attribute("ref"));
        Assert.Equal("A1:D4", (string?)sheet.Root?.Element(main + "autoFilter")?.Attribute("ref"));
        Assert.Equal("B2:B4", (string?)sheet.Descendants(main + "dataValidation").Single().Attribute("sqref"));
        Assert.Equal("E1:F1", (string?)sheet.Descendants(main + "mergeCell").Single().Attribute("ref"));
        Assert.Equal("18", (string?)sheet.Descendants(main + "col").First().Attribute("width"));
        Assert.Equal("LEN(A2)", sheet.Descendants(main + "c").Single(cell => (string?)cell.Attribute("r") == "D2").Element(main + "f")?.Value);
        var addedKeyCell = sheet.Descendants(main + "c").Single(cell => (string?)cell.Attribute("r") == "A4");
        Assert.Equal("1", (string?)addedKeyCell.Attribute("s"));
        Assert.Equal("NewKey", string.Concat(addedKeyCell.Descendants(main + "t").Select(text => text.Value)));

        var table = ReadArchiveXml(outputPath, "xl/tables/table1.xml");
        Assert.Equal("A1:D4", (string?)table.Root?.Attribute("ref"));
        Assert.Equal("A1:D4", (string?)table.Root?.Element(main + "autoFilter")?.Attribute("ref"));

        var workbook = ReadArchiveXml(outputPath, "xl/workbook.xml");
        Assert.Equal("'Settings'!$A$1:$D$4", workbook.Descendants(main + "definedName").Single().Value);
        Assert.Equal("xl", (string?)workbook.Root?.Element(main + "fileVersion")?.Attribute("appName"));
        Assert.Equal("0", (string?)workbook.Root?.Element(main + "workbookPr")?.Attribute("date1904"));
        Assert.Equal("191029", (string?)workbook.Root?.Element(main + "calcPr")?.Attribute("calcId"));
        Assert.Contains("NewKey", ReadWorkbookKeys(outputPath));
    }

    [Fact]
    public void Generate_RemovesEntryWithoutBreakingStructuredWorkbookRangesOrFormulas()
    {
        using var project = ConfigProjectFixture.Create();
        project.WriteAdvancedConfig(("RemoveKey", "remove"), ("KeepKey", "keep"));
        project.WriteWorkflow("Main.xaml", Workflow("<Sequence />"));
        var outputPath = Path.Combine(project.RootPath, "Data", "Config.Generated.xlsx");

        var result = CreateService().Generate(new UiPathConfigGenerateRequest
        {
            ProjectPath = project.RootPath,
            OutputPath = outputPath,
            RemoveKeys = ["RemoveKey"]
        });

        Assert.True(result.Success);
        var sheet = ReadArchiveXml(outputPath, "xl/worksheets/sheet1.xml");
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var removedKeyCell = sheet.Descendants(main + "c").Single(cell => (string?)cell.Attribute("r") == "A2");
        Assert.Equal("1", (string?)removedKeyCell.Attribute("s"));
        Assert.Empty(removedKeyCell.Elements());
        Assert.Equal("LEN(A2)", sheet.Descendants(main + "c").Single(cell => (string?)cell.Attribute("r") == "D2").Element(main + "f")?.Value);
        Assert.Equal("B2:B3", (string?)sheet.Descendants(main + "dataValidation").Single().Attribute("sqref"));

        var table = ReadArchiveXml(outputPath, "xl/tables/table1.xml");
        Assert.Equal("A1:D3", (string?)table.Root?.Attribute("ref"));
        var workbook = ReadArchiveXml(outputPath, "xl/workbook.xml");
        Assert.Equal("'Settings'!$A$1:$D$3", workbook.Descendants(main + "definedName").Single().Value);
        Assert.DoesNotContain("RemoveKey", ReadWorkbookKeys(outputPath));
        Assert.Contains("KeepKey", ReadWorkbookKeys(outputPath));
    }

    private static UiPathConfigAnalysisService CreateService()
    {
        return new UiPathConfigAnalysisService(new UiPathProjectScanner(), new UiPathExpressionClassifier(), new UiPathSelectorAnalyzer());
    }

    private static IReadOnlyList<string> ReadWorkbookKeys(string workbookPath)
    {
        using var archive = ZipFile.OpenRead(workbookPath);
        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheet = archive.GetEntry("xl/worksheets/sheet1.xml") ?? throw new InvalidOperationException("sheet missing");
        using var stream = sheet.Open();
        var document = XDocument.Load(stream);
        return document.Descendants(main + "row")
            .Skip(1)
            .Select(row => row.Elements(main + "c").FirstOrDefault())
            .Where(cell => cell is not null)
            .Select(cell => string.Concat(cell!.Descendants(main + "t").Select(text => text.Value)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();
    }

    private static string ReadArchiveEntry(string workbookPath, string entryPath)
    {
        using var archive = ZipFile.OpenRead(workbookPath);
        var entry = archive.GetEntry(entryPath) ?? throw new InvalidOperationException($"{entryPath} missing");
        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static XDocument ReadArchiveXml(string workbookPath, string entryPath)
    {
        return XDocument.Parse(ReadArchiveEntry(workbookPath, entryPath));
    }

    private static string Workflow(string body)
    {
        return $$"""
        <Activity
          xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:ui="http://schemas.uipath.com/workflow/activities"
          xmlns:sap2010="http://schemas.microsoft.com/netfx/2010/xaml/activities/presentation">
          {{body}}
        </Activity>
        """;
    }

    private sealed class ConfigProjectFixture : IDisposable
    {
        private ConfigProjectFixture(string rootPath)
        {
            RootPath = rootPath;
            Directory.CreateDirectory(Path.Combine(rootPath, "Data"));
            File.WriteAllText(Path.Combine(rootPath, "project.json"), """{"name":"ConfigProject","dependencies":{"UiPath.System.Activities":"[23.10.1]"}}""");
        }

        public string RootPath { get; }

        public static ConfigProjectFixture Create()
        {
            return new ConfigProjectFixture(Path.Combine(Path.GetTempPath(), $"rpadev-config-tests-{Guid.NewGuid():N}"));
        }

        public void WriteWorkflow(string relativePath, string content)
        {
            var path = Path.Combine(RootPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? RootPath);
            File.WriteAllText(path, content);
        }

        public void WriteConfig(params (string Key, string Value)[] values)
        {
            var path = Path.Combine(RootPath, "Data", "Config.xlsx");
            WriteConfigTo(path, values);
        }

        public void WriteAdvancedConfig(params (string Key, string Value)[] values)
        {
            WriteConfig(values);
            var path = Path.Combine(RootPath, "Data", "Config.xlsx");
            using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
            ReplaceText(archive, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
              <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
              <Override PartName="/xl/tables/table1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.table+xml"/>
              <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
            </Types>
            """);
            ReplaceText(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
            </Relationships>
            """);
            ReplaceText(archive, "xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <fileVersion appName="xl" lastEdited="7"/>
              <workbookPr date1904="0"/>
              <sheets><sheet name="Settings" sheetId="1" r:id="rId1"/></sheets>
              <definedNames><definedName name="ConfigRange">'Settings'!$A$1:$D$3</definedName></definedNames>
              <calcPr calcId="191029"/>
            </workbook>
            """);
            ReplaceText(archive, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
            </Relationships>
            """);
            ReplaceText(archive, "xl/worksheets/sheet1.xml", AdvancedWorksheet(values));
            AddText(archive, "xl/worksheets/_rels/sheet1.xml.rels", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/table" Target="../tables/table1.xml"/>
            </Relationships>
            """);
            AddText(archive, "xl/tables/table1.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <table xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" id="1" name="ConfigTable" displayName="ConfigTable" ref="A1:D3" totalsRowShown="0">
              <autoFilter ref="A1:D3"/>
              <tableColumns count="4"><tableColumn id="1" name="Name"/><tableColumn id="2" name="Value"/><tableColumn id="3" name="Description"/><tableColumn id="4" name="Computed"/></tableColumns>
              <tableStyleInfo name="TableStyleMedium2" showFirstColumn="0" showLastColumn="0" showRowStripes="1" showColumnStripes="0"/>
            </table>
            """);
            AddText(archive, "xl/styles.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="1"><font><name val="Aptos"/></font></fonts><fills count="1"><fill><patternFill patternType="none"/></fill></fills><borders count="1"><border/></borders><cellXfs count="2"><xf/><xf fontId="0" fillId="0" borderId="0" applyAlignment="1"><alignment wrapText="1"/></xf></cellXfs></styleSheet>
            """);
            AddText(archive, "docProps/core.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/"><dc:creator>Config Owner</dc:creator><dc:title>UiPath Config</dc:title></cp:coreProperties>
            """);
        }

        public void WriteConfigTo(string path, params (string Key, string Value)[] values)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? RootPath);
            using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
            AddText(archive, "[Content_Types].xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
              <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
            </Types>
            """);
            AddText(archive, "_rels/.rels", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
            </Relationships>
            """);
            AddText(archive, "xl/workbook.xml", """
            <?xml version="1.0" encoding="UTF-8"?>
            <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <sheets><sheet name="Settings" sheetId="1" r:id="rId1"/></sheets>
            </workbook>
            """);
            AddText(archive, "xl/_rels/workbook.xml.rels", """
            <?xml version="1.0" encoding="UTF-8"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
            </Relationships>
            """);
            AddText(archive, "xl/worksheets/sheet1.xml", Worksheet(values));
        }

        public void Dispose()
        {
            Directory.Delete(RootPath, recursive: true);
        }

        private static void AddText(ZipArchive archive, string path, string content)
        {
            var entry = archive.CreateEntry(path);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream);
            writer.Write(content);
        }

        private static void ReplaceText(ZipArchive archive, string path, string content)
        {
            archive.GetEntry(path)?.Delete();
            AddText(archive, path, content);
        }

        private static string AdvancedWorksheet((string Key, string Value)[] values)
        {
            if (values.Length != 2)
            {
                throw new ArgumentException("Advanced Config fixture requires exactly two entries.", nameof(values));
            }

            var first = values[0];
            var second = values[1];
            return $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <dimension ref="A1:F3"/>
              <cols><col min="1" max="1" width="18" customWidth="1"/><col min="2" max="4" width="24" customWidth="1"/></cols>
              <sheetData>
                <row r="1" ht="22" customHeight="1">{{Cell("A", 1, "Name")}}{{Cell("B", 1, "Value")}}{{Cell("C", 1, "Description")}}{{Cell("D", 1, "Computed")}}</row>
                <row r="2" ht="20" customHeight="1"><c r="A2" s="1" t="inlineStr"><is><t>{{System.Security.SecurityElement.Escape(first.Key)}}</t></is></c><c r="B2" s="1" t="inlineStr"><is><t>{{System.Security.SecurityElement.Escape(first.Value)}}</t></is></c><c r="C2" s="1" t="inlineStr"><is><t>first</t></is></c><c r="D2" s="1"><f>LEN(A2)</f><v>0</v></c></row>
                <row r="3" ht="20" customHeight="1"><c r="A3" s="1" t="inlineStr"><is><t>{{System.Security.SecurityElement.Escape(second.Key)}}</t></is></c><c r="B3" s="1" t="inlineStr"><is><t>{{System.Security.SecurityElement.Escape(second.Value)}}</t></is></c><c r="C3" s="1" t="inlineStr"><is><t>second</t></is></c><c r="D3" s="1"><f>LEN(A3)</f><v>0</v></c></row>
              </sheetData>
              <autoFilter ref="A1:D3"/>
              <mergeCells count="1"><mergeCell ref="E1:F1"/></mergeCells>
              <dataValidations count="1"><dataValidation type="textLength" operator="lessThanOrEqual" allowBlank="1" sqref="B2:B3"><formula1>255</formula1></dataValidation></dataValidations>
              <tableParts count="1"><tablePart r:id="rId1"/></tableParts>
            </worksheet>
            """;
        }

        private static string Worksheet((string Key, string Value)[] values)
        {
            var rows = new List<string>
            {
                Row(1, ("A", "Name"), ("B", "Value"), ("C", "Description"))
            };
            rows.AddRange(values.Select((value, index) => Row(index + 2, ("A", value.Key), ("B", value.Value), ("C", string.Empty))));
            return $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
              <sheetData>
                {{string.Join(Environment.NewLine, rows)}}
              </sheetData>
            </worksheet>
            """;
        }

        private static string Row(int number, params (string Column, string Value)[] cells)
        {
            return $"""<row r="{number}">{string.Concat(cells.Select(cell => Cell(cell.Column, number, cell.Value)))}</row>""";
        }

        private static string Cell(string column, int row, string value)
        {
            return $"""<c r="{column}{row}" t="inlineStr"><is><t>{System.Security.SecurityElement.Escape(value)}</t></is></c>""";
        }
    }
}
