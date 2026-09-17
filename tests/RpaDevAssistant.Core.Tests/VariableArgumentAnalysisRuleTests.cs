using RpaDevAssistant.Core.Analysis;
using RpaDevAssistant.Core.Analysis.Profiles;
using RpaDevAssistant.Core.Analysis.Rules;
using RpaDevAssistant.Core.Models;
using RpaDevAssistant.Core.Parsing;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class VariableArgumentAnalysisRuleTests
{
    [Theory]
    [InlineData("CustomerName", "In", true)]
    [InlineData("in_CustomerName", "In", false)]
    [InlineData("out_Result", "Out", false)]
    [InlineData("io_Item", "InOut", false)]
    public void Rpa031_ValidatesDirectionPrefix(string name, string direction, bool expectedFinding)
    {
        var workflow = Workflow();
        workflow.Arguments.Add(new UiPathArgumentInfo { Name = name, Direction = direction, Type = "String" });

        var findings = new ArgumentNamingConventionRule().Analyze(Context(workflow)).ToArray();

        Assert.Equal(expectedFinding, findings.Length == 1);
    }

    [Fact]
    public void Rpa031_UsesProfileSpecificDirectionPrefixes()
    {
        var workflow = Workflow();
        workflow.Arguments.Add(new UiPathArgumentInfo { Name = "input_CustomerName", Direction = "In", Type = "String" });
        var context = Context(workflow) with
        {
            RuleConfiguration = new UiPathRuleConfiguration
            {
                RuleId = "RPA031",
                Weight = 1,
                MaxPenalty = 5,
                NamingConvention = new UiPathNamingConventionConfiguration { InPrefix = "input_" }
            }
        };

        var findings = new ArgumentNamingConventionRule().Analyze(context).ToArray();

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("CustomerName", true)]
    [InlineData("retry_count", true)]
    [InlineData("customerName", false)]
    [InlineData("retryCount2", false)]
    public void Rpa032_ValidatesLowerCamelCase(string name, bool expectedFinding)
    {
        var workflow = Workflow();
        workflow.Variables.Add(new UiPathVariableInfo { Name = name, Type = "String", Scope = "Main" });

        var findings = new VariableNamingConventionRule().Analyze(Context(workflow)).ToArray();

        Assert.Equal(expectedFinding, findings.Length == 1);
    }

    [Fact]
    public void Rpa032_UsesProfileSpecificPatternAndPrefix()
    {
        var workflow = Workflow();
        workflow.Variables.Add(new UiPathVariableInfo { Name = "var_CustomerName", Type = "String", Scope = "Main" });
        var context = Context(workflow) with
        {
            RuleConfiguration = new UiPathRuleConfiguration
            {
                RuleId = "RPA032",
                Weight = 1,
                MaxPenalty = 5,
                NamingConvention = new UiPathNamingConventionConfiguration
                {
                    Pattern = "^var_[A-Z][A-Za-z0-9]*$",
                    RequiredPrefix = "var_"
                }
            }
        };

        var findings = new VariableNamingConventionRule().Analyze(context).ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void DefaultProfile_RegistersVariableAndArgumentNamingRules()
    {
        var profile = new BuiltInUiPathRuleProfileProvider().GetProfile("default");

        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA031" && rule.Enabled);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA032" && rule.Enabled);
    }

    [Fact]
    public void SymbolUsage_UsesExactCaseInsensitiveExpressionTokens()
    {
        var workflow = Workflow();
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?>
        {
            ["To"] = "CUSTOMERname",
            ["Value"] = "itemCount + customer.Name"
        }));

        var symbols = new UiPathSymbolUsageAnalyzer().FindReferencedSymbols(workflow);

        Assert.Contains("customerName", symbols);
        Assert.Contains("itemCount", symbols);
        Assert.Contains("customer", symbols);
        Assert.DoesNotContain("Name", symbols);
        Assert.DoesNotContain("item", symbols);
    }

    [Fact]
    public void SymbolUsage_IgnoresMetadataStringLiteralsStaticSelectorsAndCommentOut()
    {
        var workflow = Workflow();
        workflow.Activities.Add(Activity("Click", new Dictionary<string, string?>
        {
            ["DisplayName"] = "customerName",
            ["Selector"] = "<webctrl id='customerName' />",
            ["Text"] = "\"customerName\""
        }, id: "click"));
        workflow.Activities.Add(Activity("CommentOut", id: "comment"));
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?> { ["To"] = "customerName" }, id: "disabled", parentId: "comment"));

        var symbols = new UiPathSymbolUsageAnalyzer().FindReferencedSymbols(workflow);

        Assert.DoesNotContain("customerName", symbols);
    }

    [Fact]
    public void Rpa033_ReportsOnlyUnreferencedNonShadowedVariable()
    {
        var workflow = Workflow();
        workflow.Variables.Add(new UiPathVariableInfo { Name = "usedValue" });
        workflow.Variables.Add(new UiPathVariableInfo { Name = "unusedValue" });
        workflow.Variables.Add(new UiPathVariableInfo { Name = "shadowed" });
        workflow.Variables.Add(new UiPathVariableInfo { Name = "SHADOWED" });
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?> { ["To"] = "usedValue" }));

        var findings = new UnusedVariableRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(workflow)).ToArray();

        var finding = Assert.Single(findings);
        Assert.Equal("RPA033", finding.RuleId);
        Assert.Equal("unusedValue", finding.CurrentValue);
    }

    [Fact]
    public void Rpa033_ResolvesShadowedVariablesByStableScope()
    {
        var workflow = Workflow();
        workflow.Variables.Add(new UiPathVariableInfo { Name = "status", Scope = "Outer", ScopeActivityId = "outer" });
        workflow.Variables.Add(new UiPathVariableInfo { Name = "STATUS", Scope = "Inner", ScopeActivityId = "inner" });
        workflow.Activities.Add(Activity("Sequence", id: "outer"));
        workflow.Activities.Add(Activity("Sequence", id: "inner", parentId: "outer"));
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?> { ["Value"] = "CStr([status]).Trim()" }, id: "inner-use", parentId: "inner"));

        var findings = new UnusedVariableRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(workflow)).ToArray();

        Assert.Single(findings);
        Assert.Equal("status", findings[0].CurrentValue);
    }

    [Fact]
    public void Rpa033_KeepsFailSafeForShadowedVariablesWithoutStableScopes()
    {
        var workflow = Workflow();
        workflow.Variables.Add(new UiPathVariableInfo { Name = "status", Scope = "Outer" });
        workflow.Variables.Add(new UiPathVariableInfo { Name = "STATUS", Scope = "Inner" });

        var findings = new UnusedVariableRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(workflow)).ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa034_ReportsUnusedArgumentAndKeepsReferencedArgument()
    {
        var workflow = Workflow();
        workflow.Arguments.Add(new UiPathArgumentInfo { Name = "in_Used", Direction = "In" });
        workflow.Arguments.Add(new UiPathArgumentInfo { Name = "in_Unused", Direction = "In" });
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?> { ["Value"] = "in_used.Trim()" }));

        var findings = new UnusedArgumentRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(workflow)).ToArray();

        var finding = Assert.Single(findings);
        Assert.Equal("RPA034", finding.RuleId);
        Assert.Equal("in_Unused", finding.CurrentValue);
    }

    [Fact]
    public void Rpa034_KeepsArgumentMappedByStaticCallerContract()
    {
        var caller = Workflow("Main.xaml");
        caller.Activities.Add(Invoke("Business\\Child.xaml", new Dictionary<string, string?>
        {
            ["IN_INPUT"] = "[sourceValue]"
        }));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "in_Input", Direction = "In" });

        var findings = new UnusedArgumentRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(caller, callee)).ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa034_ReportsUnmappedCalleeArgument()
    {
        var caller = Workflow("Main.xaml");
        caller.Activities.Add(Invoke("Business/Child.xaml", new Dictionary<string, string?>
        {
            ["in_Other"] = "[sourceValue]"
        }));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "in_Input", Direction = "In" });

        var finding = Assert.Single(new UnusedArgumentRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(caller, callee)));

        Assert.Equal("in_Input", finding.CurrentValue);
        Assert.Equal("Business/Child.xaml", finding.WorkflowPath);
    }

    [Fact]
    public void Rpa034_KeepsFailSafeForRuntimeArgumentsVariable()
    {
        var caller = Workflow("Main.xaml");
        caller.Activities.Add(Invoke("Business/Child.xaml", argumentsVariable: "[workflowArguments]"));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "in_RuntimeValue", Direction = "In" });

        var findings = new UnusedArgumentRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(caller, callee)).ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa034_ResolvesCallerRelativeWorkflowPath()
    {
        var caller = Workflow("Framework/Caller.xaml");
        caller.Activities.Add(Invoke("..\\Business\\Child.xaml", new Dictionary<string, string?>
        {
            ["in_Input"] = "[sourceValue]"
        }));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "in_Input", Direction = "In" });

        var findings = new UnusedArgumentRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(caller, callee)).ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa034_AcceptsMappingsFromMultipleCallers()
    {
        var firstCaller = Workflow("Main.xaml");
        firstCaller.Activities.Add(Invoke("Business/Child.xaml", new Dictionary<string, string?>
        {
            ["in_First"] = "[firstValue]"
        }));
        var secondCaller = Workflow("Framework/Caller.xaml");
        secondCaller.Activities.Add(Invoke("../Business/Child.xaml", new Dictionary<string, string?>
        {
            ["in_Second"] = "[secondValue]"
        }));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "in_First", Direction = "In" });
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "in_Second", Direction = "In" });

        var findings = new UnusedArgumentRule(new UiPathSymbolUsageAnalyzer())
            .Analyze(Context(firstCaller, secondCaller, callee))
            .ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa043_ReportsUnknownStaticInvokeMappingKey()
    {
        var caller = Workflow("Main.xaml");
        caller.Activities.Add(Invoke("Business/Child.xaml", new Dictionary<string, string?>
        {
            ["in_Unknown"] = "[value]"
        }));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "in_Input", Direction = "In" });

        var finding = Assert.Single(new InvalidInvokeWorkflowArgumentMappingRule().Analyze(Context(caller, callee)));

        Assert.Equal("RPA043", finding.RuleId);
        Assert.Equal("in_Unknown", finding.CurrentValue);
        Assert.Equal("Main.xaml", finding.WorkflowPath);
    }

    [Fact]
    public void Rpa044_ReportsMissingInputButNotMissingOutputMapping()
    {
        var caller = Workflow("Framework/Caller.xaml");
        caller.Activities.Add(Invoke("..\\Business\\Child.xaml"));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "in_Input", Direction = "In", Type = "InArgument(x:String)" });
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "io_State", Direction = "InOut", Type = "InOutArgument(x:String)" });
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "out_Result", Direction = "Out", Type = "OutArgument(x:String)" });

        var findings = new MissingInvokeWorkflowArgumentMappingRule().Analyze(Context(caller, callee)).ToArray();

        Assert.Equal(2, findings.Length);
        Assert.Contains(findings, finding => finding.CurrentValue == "in_Input");
        Assert.Contains(findings, finding => finding.CurrentValue == "io_State");
        Assert.DoesNotContain(findings, finding => finding.CurrentValue == "out_Result");
    }

    [Fact]
    public void Rpa044_DoesNotReportUnmappedInputWithSerializedDefault()
    {
        var caller = Workflow("Main.xaml");
        caller.Activities.Add(Invoke("Business/Child.xaml"));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo
        {
            Name = "in_Optional",
            Direction = "In",
            Type = "InArgument(x:String)",
            DefaultValue = "[\"fallback\"]",
            HasDefaultValue = true
        });

        var findings = new MissingInvokeWorkflowArgumentMappingRule().Analyze(Context(caller, callee));

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa044_UsesParsedDefaultValueEvidenceEndToEnd()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), $"InvokeDefaultTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectPath, "Business"));
        try
        {
            var callerPath = Path.Combine(projectPath, "Main.xaml");
            var calleePath = Path.Combine(projectPath, "Business", "Child.xaml");
            File.WriteAllText(callerPath, WorkflowXaml("""
              <Sequence>
                <ui:InvokeWorkflowFile WorkflowFileName="Business/Child.xaml" />
              </Sequence>
            """));
            File.WriteAllText(calleePath, WorkflowXaml("""
              <x:Members>
                <x:Property Name="in_Optional" Type="InArgument(x:String)" />
              </x:Members>
              <this:Main.in_Optional xmlns:this="clr-namespace:">
                <InArgument x:TypeArguments="x:String">["fallback"]</InArgument>
              </this:Main.in_Optional>
              <Sequence />
            """));

            var parser = new UiPathXamlParser();
            var callee = parser.Parse(calleePath, projectPath);
            var context = Context(parser.Parse(callerPath, projectPath), callee);

            Assert.True(Assert.Single(callee.Arguments).HasDefaultValue);
            Assert.Empty(new MissingInvokeWorkflowArgumentMappingRule().Analyze(context));
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void Rpa045_ReportsMappingDirectionMismatchAndKeepsMatchingDirection()
    {
        var caller = Workflow("Main.xaml");
        caller.Activities.Add(Invoke(
            "Business/Child.xaml",
            new Dictionary<string, string?>
            {
                ["in_Input"] = "[input]",
                ["out_Result"] = "[result]"
            },
            directions: new Dictionary<string, string?>
            {
                ["in_Input"] = "In",
                ["out_Result"] = "In"
            }));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "in_Input", Direction = "In", Type = "InArgument(x:String)" });
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "out_Result", Direction = "Out", Type = "OutArgument(x:String)" });

        var finding = Assert.Single(new InvokeWorkflowArgumentDirectionMismatchRule().Analyze(Context(caller, callee)));

        Assert.Equal("RPA045", finding.RuleId);
        Assert.Contains("out_Result", finding.Message);
    }

    [Fact]
    public void InvokeContractRules_SkipRuntimeArgumentDictionary()
    {
        var caller = Workflow("Main.xaml");
        caller.Activities.Add(Invoke(
            "Business/Child.xaml",
            new Dictionary<string, string?> { ["in_Unknown"] = "[value]" },
            argumentsVariable: "[workflowArguments]",
            directions: new Dictionary<string, string?> { ["in_Unknown"] = "Out" }));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "in_Input", Direction = "In" });

        var context = Context(caller, callee);

        Assert.Empty(new InvalidInvokeWorkflowArgumentMappingRule().Analyze(context));
        Assert.Empty(new MissingInvokeWorkflowArgumentMappingRule().Analyze(context));
        Assert.Empty(new InvokeWorkflowArgumentDirectionMismatchRule().Analyze(context));
    }

    [Fact]
    public void InvokeContractRules_SkipDynamicWorkflowReference()
    {
        var caller = Workflow("Main.xaml");
        caller.Activities.Add(Invoke("[workflowPath]", new Dictionary<string, string?> { ["in_Unknown"] = "[value]" }));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "in_Input", Direction = "In" });

        var context = Context(caller, callee);

        Assert.Empty(new InvalidInvokeWorkflowArgumentMappingRule().Analyze(context));
        Assert.Empty(new MissingInvokeWorkflowArgumentMappingRule().Analyze(context));
        Assert.Empty(new InvokeWorkflowArgumentDirectionMismatchRule().Analyze(context));
    }

    [Fact]
    public void InvokeContractRules_TreatNullArgumentsVariableAsNoRuntimeDictionary()
    {
        var caller = Workflow("Main.xaml");
        caller.Activities.Add(Invoke("Business/Child.xaml", argumentsVariable: "{x:Null}"));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo
        {
            Name = "in_Input",
            Direction = "In",
            Type = "InArgument(x:String)"
        });

        var finding = Assert.Single(new MissingInvokeWorkflowArgumentMappingRule().Analyze(Context(caller, callee)));

        Assert.Equal("in_Input", finding.CurrentValue);
    }

    [Fact]
    public void InvokeContractRules_SkipAmbiguousDuplicateArgumentDeclarations()
    {
        var caller = Workflow("Main.xaml");
        caller.Activities.Add(Invoke(
            "Business/Child.xaml",
            new Dictionary<string, string?> { ["value"] = "[source]" },
            directions: new Dictionary<string, string?> { ["value"] = "Out" }));
        var callee = Workflow("Business/Child.xaml");
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "value", Direction = "In", Type = "InArgument(x:String)" });
        callee.Arguments.Add(new UiPathArgumentInfo { Name = "VALUE", Direction = "Out", Type = "OutArgument(x:String)" });

        var context = Context(caller, callee);

        Assert.Empty(new InvalidInvokeWorkflowArgumentMappingRule().Analyze(context));
        Assert.Empty(new MissingInvokeWorkflowArgumentMappingRule().Analyze(context));
        Assert.Empty(new InvokeWorkflowArgumentDirectionMismatchRule().Analyze(context));
    }

    [Fact]
    public void InvokeContractRules_UseParserMappingKeysAndDirectionsEndToEnd()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), $"InvokeContractTests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(projectPath, "Business"));
        try
        {
            var callerPath = Path.Combine(projectPath, "Main.xaml");
            var calleePath = Path.Combine(projectPath, "Business", "Child.xaml");
            File.WriteAllText(callerPath, WorkflowXaml("""
              <Sequence>
                <ui:InvokeWorkflowFile WorkflowFileName="Business\Child.xaml">
                  <ui:InvokeWorkflowFile.Arguments>
                    <scg:Dictionary x:TypeArguments="x:String, Argument">
                      <OutArgument x:Key="in_Input">[input]</OutArgument>
                      <InArgument x:Key="in_Unknown">[other]</InArgument>
                    </scg:Dictionary>
                  </ui:InvokeWorkflowFile.Arguments>
                </ui:InvokeWorkflowFile>
              </Sequence>
            """));
            File.WriteAllText(calleePath, WorkflowXaml("""
              <x:Members>
                <x:Property Name="in_Input" Type="InArgument(x:String)" />
              </x:Members>
              <Sequence />
            """));

            var parser = new UiPathXamlParser();
            var context = Context(parser.Parse(callerPath, projectPath), parser.Parse(calleePath, projectPath));

            Assert.Equal("in_Unknown", Assert.Single(new InvalidInvokeWorkflowArgumentMappingRule().Analyze(context)).CurrentValue);
            Assert.Equal("Out", Assert.Single(new InvokeWorkflowArgumentDirectionMismatchRule().Analyze(context)).CurrentValue);
            Assert.Empty(new MissingInvokeWorkflowArgumentMappingRule().Analyze(context));
        }
        finally
        {
            Directory.Delete(projectPath, recursive: true);
        }
    }

    [Fact]
    public void Rpa034_DoesNotResolveAmbiguousNormalizedWorkflowPaths()
    {
        var caller = Workflow("Main.xaml");
        caller.Activities.Add(Invoke("Business/Child.xaml", new Dictionary<string, string?>
        {
            ["in_Input"] = "[sourceValue]"
        }));
        var firstCallee = Workflow("Business/Child.xaml");
        firstCallee.Arguments.Add(new UiPathArgumentInfo { Name = "in_Input", Direction = "In" });
        var duplicateCallee = Workflow("Business\\Child.xaml");
        duplicateCallee.Arguments.Add(new UiPathArgumentInfo { Name = "in_Input", Direction = "In" });

        var findings = new UnusedArgumentRule(new UiPathSymbolUsageAnalyzer())
            .Analyze(Context(caller, firstCallee, duplicateCallee))
            .ToArray();

        Assert.Equal(2, findings.Count(finding => finding.CurrentValue == "in_Input"));
    }

    [Fact]
    public void SymbolUsage_ReadsInvokeWorkflowArgumentMappings()
    {
        var workflow = Workflow();
        workflow.Activities.Add(new UiPathActivityInfo
        {
            ActivityId = "invoke",
            Name = "InvokeWorkflowFile",
            DisplayName = "Invoke Process",
            TypeName = "InvokeWorkflowFile",
            XamlFile = "Main.xaml",
            Arguments = new Dictionary<string, string?>
            {
                ["in_Config"] = "[config]",
                ["out_Result"] = "[processResult]"
            }
        });

        var symbols = new UiPathSymbolUsageAnalyzer().FindReferencedSymbols(workflow);

        Assert.Contains("Config", symbols);
        Assert.Contains("processResult", symbols);
        Assert.DoesNotContain("in_Config", symbols);
        Assert.DoesNotContain("out_Result", symbols);
    }

    [Fact]
    public void SymbolUsage_DistinguishesReadAndWriteAccess()
    {
        var workflow = Workflow();
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?>
        {
            ["To"] = "out_Result",
            ["Value"] = "in_Source.Trim()"
        }));

        var analyzer = new UiPathSymbolUsageAnalyzer();

        Assert.Equal(UiPathSymbolAccess.Read, analyzer.GetArgumentAccess(workflow, "in_Source"));
        Assert.Equal(UiPathSymbolAccess.Write, analyzer.GetArgumentAccess(workflow, "out_Result"));
    }

    [Fact]
    public void SymbolUsage_TreatsNonAssignmentToPropertyAsReadAccess()
    {
        var workflow = Workflow();
        workflow.Activities.Add(Activity("SendSmtpMailMessage", new Dictionary<string, string?>
        {
            ["To"] = "in_Recipient"
        }));

        var access = new UiPathSymbolUsageAnalyzer().GetArgumentAccess(workflow, "in_Recipient");

        Assert.Equal(UiPathSymbolAccess.Read, access);
    }

    [Fact]
    public void SymbolUsage_IgnoresStaticSelectorTextButReadsDynamicSelectorExpression()
    {
        var workflow = Workflow();
        workflow.Activities.Add(Activity("Click", new Dictionary<string, string?>
        {
            ["Selector"] = "<webctrl id='in_TargetName' tag='BUTTON' />"
        }));
        workflow.Activities.Add(Activity("Click", new Dictionary<string, string?>
        {
            ["Selector"] = "\"<webctrl aaname='\" + in_DynamicName + \"' tag='BUTTON' />\""
        }));

        var analyzer = new UiPathSymbolUsageAnalyzer();

        Assert.Equal(UiPathSymbolAccess.None, analyzer.GetArgumentAccess(workflow, "in_TargetName"));
        Assert.Equal(UiPathSymbolAccess.Read, analyzer.GetArgumentAccess(workflow, "in_DynamicName"));
    }

    [Fact]
    public void SymbolUsage_DoesNotGuessDirectionForUnprefixedInvokeMapping()
    {
        var workflow = Workflow();
        workflow.Activities.Add(Invoke("Child.xaml", new Dictionary<string, string?>
        {
            ["customer"] = "[out_Result]"
        }));

        var access = new UiPathSymbolUsageAnalyzer().GetArgumentAccess(workflow, "out_Result");

        Assert.Equal(UiPathSymbolAccess.None, access);
    }

    [Fact]
    public void Rpa038_IgnoresWritesInsideCommentOut()
    {
        var workflow = Workflow();
        workflow.Arguments.Add(new UiPathArgumentInfo { Name = "in_Value", Direction = "In" });
        workflow.Activities.Add(Activity("CommentOut", id: "comment"));
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?>
        {
            ["To"] = "in_Value"
        }, id: "disabled", parentId: "comment"));

        var findings = new ArgumentDirectionMismatchRule(new UiPathSymbolUsageAnalyzer())
            .Analyze(Context(workflow))
            .ToArray();

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("In", "To", true)]
    [InlineData("In", "Value", false)]
    [InlineData("Out", "Value", true)]
    [InlineData("Out", "To", false)]
    public void Rpa038_ReportsOnlyDirectionAccessMismatch(string direction, string propertyName, bool expectedFinding)
    {
        var workflow = Workflow();
        workflow.Arguments.Add(new UiPathArgumentInfo { Name = "argumentValue", Direction = direction });
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?>
        {
            [propertyName] = "argumentValue"
        }));

        var findings = new ArgumentDirectionMismatchRule(new UiPathSymbolUsageAnalyzer())
            .Analyze(Context(workflow))
            .ToArray();

        Assert.Equal(expectedFinding, findings.Length == 1);
    }

    [Theory]
    [InlineData("Value", "In")]
    [InlineData("To", "Out")]
    public void Rpa039_SuggestsNarrowDirectionForSingleAccess(string propertyName, string expectedDirection)
    {
        var workflow = Workflow();
        workflow.Arguments.Add(new UiPathArgumentInfo { Name = "io_Value", Direction = "InOut" });
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?>
        {
            [propertyName] = "io_Value"
        }));

        var finding = Assert.Single(new UnnecessaryInOutArgumentRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(workflow)));

        Assert.Equal("RPA039", finding.RuleId);
        Assert.Contains(expectedDirection, finding.Recommendation, StringComparison.Ordinal);
    }

    [Fact]
    public void Rpa039_DoesNotReportReadWriteOrUnusedInOutArgument()
    {
        var workflow = Workflow();
        workflow.Arguments.Add(new UiPathArgumentInfo { Name = "io_Used", Direction = "InOut" });
        workflow.Arguments.Add(new UiPathArgumentInfo { Name = "io_Unused", Direction = "InOut" });
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?>
        {
            ["To"] = "io_Used",
            ["Value"] = "io_Used + 1"
        }));

        var findings = new UnnecessaryInOutArgumentRule(new UiPathSymbolUsageAnalyzer())
            .Analyze(Context(workflow))
            .ToArray();

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData("InArgument(x:String)")]
    [InlineData("OutArgument(scg:Dictionary(x:String, x:Object))")]
    [InlineData("InOutArgument(sd:DataRow)")]
    [InlineData("InArgument(s:String[])")]
    [InlineData("InArgument(scg:Dictionary(x:String, scg:List(x:Int32)))")]
    [InlineData("InArgument(x:Int32?)")]
    public void Rpa040_AcceptsValidArgumentTypeDeclarations(string type)
    {
        var workflow = Workflow();
        workflow.Arguments.Add(new UiPathArgumentInfo { Name = "argumentValue", Direction = "In", Type = type });

        var findings = new InvalidArgumentTypeDeclarationRule().Analyze(Context(workflow)).ToArray();

        Assert.Empty(findings);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x:String")]
    [InlineData("InArgument()")]
    [InlineData("UnknownArgument(x:String)")]
    [InlineData("InArgument( )")]
    [InlineData("InArgument(Not A Type)")]
    [InlineData("InArgument(x:String))")]
    public void Rpa040_ReportsMissingOrMalformedArgumentType(string? type)
    {
        var workflow = Workflow();
        workflow.Arguments.Add(new UiPathArgumentInfo { Name = "argumentValue", Direction = "In", Type = type });

        var finding = Assert.Single(new InvalidArgumentTypeDeclarationRule().Analyze(Context(workflow)));

        Assert.Equal("RPA040", finding.RuleId);
        Assert.Equal("ArgumentType", finding.PropertyName);
    }

    [Fact]
    public void Rpa041_ReportsVariableUsedOnlyInNarrowerSequence()
    {
        var workflow = Workflow();
        workflow.Variables.Add(new UiPathVariableInfo
        {
            Name = "customerName",
            Scope = "Outer",
            ScopeActivityId = "outer"
        });
        workflow.Activities.Add(Activity("Sequence", id: "outer"));
        workflow.Activities.Add(Activity("Sequence", id: "inner", parentId: "outer"));
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?>
        {
            ["Value"] = "customerName.Trim()"
        }, id: "use", parentId: "inner"));

        var finding = Assert.Single(new OverlyBroadVariableScopeRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(workflow)));

        Assert.Equal("RPA041", finding.RuleId);
        Assert.Equal("inner", finding.ActivityId);
        Assert.Equal("VariableScope", finding.PropertyName);
    }

    [Fact]
    public void Rpa041_DoesNotReportVariableUsedAcrossSiblingScopes()
    {
        var workflow = Workflow();
        workflow.Variables.Add(new UiPathVariableInfo { Name = "value", ScopeActivityId = "outer" });
        workflow.Activities.Add(Activity("Sequence", id: "outer"));
        workflow.Activities.Add(Activity("Sequence", id: "left", parentId: "outer"));
        workflow.Activities.Add(Activity("Sequence", id: "right", parentId: "outer"));
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?> { ["Value"] = "value" }, id: "left-use", parentId: "left"));
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?> { ["Value"] = "value" }, id: "right-use", parentId: "right"));

        var findings = new OverlyBroadVariableScopeRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(workflow)).ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa041_DoesNotSuggestNonContainerActivityScope()
    {
        var workflow = Workflow();
        workflow.Variables.Add(new UiPathVariableInfo { Name = "value", ScopeActivityId = "outer" });
        workflow.Activities.Add(Activity("Sequence", id: "outer"));
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?> { ["Value"] = "value" }, id: "use", parentId: "outer"));

        var findings = new OverlyBroadVariableScopeRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(workflow)).ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void Rpa041_IncludesVariableDefaultReferencesInScopeCalculation()
    {
        var workflow = Workflow();
        workflow.Variables.Add(new UiPathVariableInfo { Name = "value", ScopeActivityId = "outer" });
        workflow.Variables.Add(new UiPathVariableInfo
        {
            Name = "derivedValue",
            ScopeActivityId = "outer",
            DefaultValue = "value.Trim()"
        });
        workflow.Activities.Add(Activity("Sequence", id: "outer"));
        workflow.Activities.Add(Activity("Sequence", id: "inner", parentId: "outer"));
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?> { ["Value"] = "value" }, id: "use", parentId: "inner"));

        var findings = new OverlyBroadVariableScopeRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(workflow)).ToArray();

        Assert.DoesNotContain(findings, finding => finding.CurrentValue == "value");
    }

    [Fact]
    public void ScopeRulesFailSafeForParentCycleOrMissingParent()
    {
        var workflow = Workflow();
        workflow.Variables.Add(new UiPathVariableInfo { Name = "value", ScopeActivityId = "first" });
        workflow.Variables.Add(new UiPathVariableInfo { Name = "VALUE", ScopeActivityId = "second" });
        workflow.Activities.Add(Activity("Sequence", id: "first", parentId: "second"));
        workflow.Activities.Add(Activity("Sequence", id: "second", parentId: "first"));
        workflow.Activities.Add(Activity("Assign", new Dictionary<string, string?> { ["Value"] = "value" }, id: "orphan", parentId: "missing"));

        Assert.Empty(new OverlyBroadVariableScopeRule(new UiPathSymbolUsageAnalyzer()).Analyze(Context(workflow)));
        Assert.Empty(new ShadowedVariableRule().Analyze(Context(workflow)));
    }

    [Fact]
    public void Rpa042_ReportsNestedVariableShadowing()
    {
        var workflow = Workflow();
        workflow.Variables.Add(new UiPathVariableInfo { Name = "status", Scope = "Outer", ScopeActivityId = "outer" });
        workflow.Variables.Add(new UiPathVariableInfo { Name = "STATUS", Scope = "Inner", ScopeActivityId = "inner" });
        workflow.Activities.Add(Activity("Sequence", id: "outer"));
        workflow.Activities.Add(Activity("Sequence", id: "inner", parentId: "outer"));

        var finding = Assert.Single(new ShadowedVariableRule().Analyze(Context(workflow)));

        Assert.Equal("RPA042", finding.RuleId);
        Assert.Equal("inner", finding.ActivityId);
    }

    [Fact]
    public void Rpa042_DoesNotReportSameNameInSiblingOrUnstableScopes()
    {
        var workflow = Workflow();
        workflow.Variables.Add(new UiPathVariableInfo { Name = "status", ScopeActivityId = "left" });
        workflow.Variables.Add(new UiPathVariableInfo { Name = "STATUS", ScopeActivityId = "right" });
        workflow.Variables.Add(new UiPathVariableInfo { Name = "status" });
        workflow.Activities.Add(Activity("Sequence", id: "outer"));
        workflow.Activities.Add(Activity("Sequence", id: "left", parentId: "outer"));
        workflow.Activities.Add(Activity("Sequence", id: "right", parentId: "outer"));

        var findings = new ShadowedVariableRule().Analyze(Context(workflow)).ToArray();

        Assert.Empty(findings);
    }

    [Fact]
    public void DefaultProfile_RegistersVariableArgumentRules()
    {
        var profile = new BuiltInUiPathRuleProfileProvider().GetProfile("default");

        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA033" && rule.Enabled);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA034" && rule.Enabled);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA038" && rule.Enabled);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA039" && rule.Enabled);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA040" && rule.Enabled);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA041" && rule.Enabled);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA042" && rule.Enabled);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA043" && rule.Enabled);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA044" && rule.Enabled);
        Assert.Contains(profile.Rules, rule => rule.RuleId == "RPA045" && rule.Enabled);
    }

    private static UiPathWorkflowAnalysis Workflow(string relativePath = "Main.xaml") => new()
    {
        FileName = Path.GetFileName(relativePath),
        RelativePath = relativePath
    };

    private static UiPathActivityInfo Invoke(
        string workflowPath,
        IReadOnlyDictionary<string, string?>? arguments = null,
        string? argumentsVariable = null,
        IReadOnlyDictionary<string, string?>? directions = null) => new()
    {
        ActivityId = Guid.NewGuid().ToString("N"),
        Name = "InvokeWorkflowFile",
        DisplayName = "Invoke Workflow File",
        TypeName = "InvokeWorkflowFile",
        XamlFile = "Main.xaml",
        Properties = new Dictionary<string, string?>
        {
            ["WorkflowFileName"] = workflowPath,
            ["ArgumentsVariable"] = argumentsVariable
        },
        Arguments = arguments ?? new Dictionary<string, string?>(),
        ArgumentMappingDirections = directions ?? new Dictionary<string, string?>()
    };

    private static UiPathActivityInfo Activity(
        string name,
        IReadOnlyDictionary<string, string?>? properties = null,
        string? id = null,
        string? parentId = null) => new()
    {
        ActivityId = id ?? Guid.NewGuid().ToString("N"),
        ParentActivityId = parentId,
        Name = name,
        DisplayName = name,
        TypeName = name,
        XamlFile = "Main.xaml",
        Properties = properties ?? new Dictionary<string, string?>()
    };

    private static UiPathAnalysisContext Context(params UiPathWorkflowAnalysis[] workflows)
    {
        var project = new ProjectScanResult
        {
            ProjectPath = "/tmp/project",
            ProjectName = "TestProject",
            Compatibility = "Windows"
        };
        project.ProjectFolderExists = true;
        project.ProjectJsonExists = true;
        project.ProjectJsonParsed = true;
        foreach (var workflow in workflows)
        {
            project.Workflows.Add(new UiPathWorkflowInfo
            {
                Name = workflow.FileName,
                RelativePath = workflow.RelativePath,
                FullPath = Path.Combine(project.ProjectPath, workflow.RelativePath),
                Analysis = workflow
            });
        }

        return new UiPathAnalysisContext { Project = project };
    }

    private static string WorkflowXaml(string body) => $$"""
        <Activity
          xmlns="http://schemas.microsoft.com/netfx/2009/xaml/activities"
          xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
          xmlns:ui="http://schemas.uipath.com/workflow/activities"
          xmlns:scg="clr-namespace:System.Collections.Generic;assembly=System.Private.CoreLib"
          x:Class="Main">
        {{body}}
        </Activity>
        """;
}
