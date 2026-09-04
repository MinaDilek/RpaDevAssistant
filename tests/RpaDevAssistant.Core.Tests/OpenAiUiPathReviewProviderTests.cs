using RpaDevAssistant.Core.Ai;
using RpaDevAssistant.Infrastructure.OpenAI;
using Xunit;

namespace RpaDevAssistant.Core.Tests;

public sealed class OpenAiUiPathReviewProviderTests
{
    [Fact]
    public void ParseResponsePayload_ParsesStructuredModelResponse()
    {
        var payload = """
        {
          "model": "gpt-test",
          "output_text": "{\"summary\":\"Good\",\"riskLevel\":\"Low\",\"strengths\":[\"Clear workflow\"],\"issues\":[],\"recommendations\":[\"Keep logging\"],\"architectureObservations\":[],\"confidence\":0.7}",
          "usage": {
            "input_tokens": 10,
            "output_tokens": 20,
            "total_tokens": 30
          }
        }
        """;

        var result = OpenAiUiPathReviewProvider.ParseResponsePayload(payload, Prompt());

        Assert.True(result.IsSuccess);
        Assert.Equal("Good", result.Summary);
        Assert.Equal("gpt-test", result.Model);
        Assert.Equal(30, result.TotalTokens);
    }

    [Fact]
    public void ParseResponsePayload_ReturnsFailureForInvalidStructuredResponse()
    {
        var result = OpenAiUiPathReviewProvider.ParseResponsePayload("{ invalid", Prompt());

        Assert.False(result.IsSuccess);
        Assert.Contains("invalid JSON", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static UiPathAiPrompt Prompt()
    {
        return new UiPathAiPrompt
        {
            Scope = UiPathAiReviewScope.Project,
            SystemInstructions = "system",
            UserContext = "context"
        };
    }
}
