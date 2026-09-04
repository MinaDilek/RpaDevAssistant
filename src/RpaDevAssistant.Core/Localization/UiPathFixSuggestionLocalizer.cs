using RpaDevAssistant.Core.Fixes;

namespace RpaDevAssistant.Core.Localization;

public sealed class UiPathFixSuggestionLocalizer
{
    private static readonly IReadOnlyDictionary<string, string> TurkishFixText = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Add logging at workflow start/end."] = "Workflow başlangıcı ve bitişine logging ekleyin.",
        ["Log external calls and important business decisions."] = "External call ve önemli business decision noktalarını loglayın.",
        ["Avoid logging credentials, tokens, or personal data."] = "Credential, token veya personal data loglamayın.",
        ["Overly verbose logging can make production logs noisy."] = "Aşırı detaylı logging production loglarını gürültülü hale getirebilir.",
        ["Logging sensitive values can create security exposure."] = "Sensitive value loglamak güvenlik riski oluşturabilir.",
        ["Avoid logging sensitive values."] = "Sensitive value loglamaktan kaçının.",
        ["Use consistent log levels and include useful business context."] = "Tutarlı log level kullanın ve yararlı business context ekleyin.",
        ["Choose workflow boundaries and business events worth logging."] = "Loglanmaya değer workflow boundary ve business event’leri seçin.",
        ["Identify the expected UI or application state."] = "Beklenen UI veya application state’i belirleyin.",
        ["Replace the fixed delay with Check App State, Retry Scope, Element Exists, or a timeout-based UI activity."] = "Sabit Delay’i Check App State, Retry Scope, Element Exists veya timeout tabanlı bir UI activity ile değiştirin.",
        ["Keep timeout values configurable."] = "Timeout değerlerini configurable tutun.",
        ["The correct target state cannot be inferred safely from static XAML alone."] = "Doğru target state yalnız statik XAML’den güvenle çıkarılamaz.",
        ["Changing wait behavior can affect timing-sensitive workflows."] = "Wait davranışını değiştirmek timing-sensitive workflow’ları etkileyebilir.",
        ["Verify the target state in UiPath Studio."] = "Target state’i UiPath Studio içinde doğrulayın.",
        ["Use configurable timeout values where possible."] = "Mümkün olduğunda configurable timeout değerleri kullanın.",
        ["Select the UI/application state that indicates readiness."] = "Hazır olma durumunu gösteren UI/application state’i seçin.",
        ["Add a Log Message with exception context."] = "Exception context içeren bir Log Message ekleyin.",
        ["Decide whether the exception should be rethrown, transformed, or explicitly handled."] = "Exception’ın rethrow, transform veya explicit handle edilmesi gerekip gerekmediğine karar verin.",
        ["Verify transaction status behavior after the change."] = "Değişiklikten sonra transaction status davranışını doğrulayın.",
        ["Adding Rethrow when the exception is expected can change business flow."] = "Beklenen exception için Rethrow eklemek business flow’u değiştirebilir.",
        ["Handling without propagation can hide real failures if not designed carefully."] = "Dikkatli tasarlanmazsa propagation olmadan handling gerçek hataları gizleyebilir.",
        ["Review expected versus unexpected exception behavior."] = "Expected ve unexpected exception davranışını gözden geçirin.",
        ["Test in UiPath Studio."] = "UiPath Studio içinde test edin.",
        ["Verify exception propagation and transaction status handling."] = "Exception propagation ve transaction status handling davranışını doğrulayın.",
        ["Decide whether this Catch handles an expected business exception or an unexpected application failure."] = "Bu Catch bloğunun expected business exception mı yoksa unexpected application failure mı ele aldığını belirleyin.",
        ["Choose a descriptive PascalCase workflow name."] = "Açıklayıcı PascalCase bir workflow adı seçin.",
        ["Update Invoke Workflow File references that point to the old file."] = "Eski dosyayı gösteren Invoke Workflow File referanslarını güncelleyin.",
        ["Re-run analysis to verify references."] = "Referansları doğrulamak için analizi yeniden çalıştırın.",
        ["Existing Invoke Workflow File references may need to be updated."] = "Mevcut Invoke Workflow File referanslarının güncellenmesi gerekebilir.",
        ["External documentation or tests may reference the old filename."] = "External documentation veya testler eski dosya adına referans veriyor olabilir.",
        ["Rename references to this workflow before changing the file name."] = "Dosya adını değiştirmeden önce bu workflow’a verilen referansları güncelleyin.",
        ["Verify Invoke Workflow File references after renaming."] = "Yeniden adlandırma sonrası Invoke Workflow File referanslarını doğrulayın.",
        ["Provide the intended workflow responsibility as a PascalCase name."] = "Amaçlanan workflow sorumluluğunu PascalCase isim olarak belirtin.",
        ["Confirm which workflow should be invoked."] = "Hangi workflow’un çağrılması gerektiğini doğrulayın.",
        ["Update the Invoke Workflow File path in UiPath Studio."] = "Invoke Workflow File path’ini UiPath Studio içinde güncelleyin.",
        ["Re-run analysis to verify the reference resolves."] = "Referansın çözümlendiğini doğrulamak için analizi yeniden çalıştırın.",
        ["Choosing the wrong workflow target can change business behavior."] = "Yanlış workflow target seçmek business behavior’ı değiştirebilir.",
        ["Renamed workflows may require multiple Invoke Workflow File references to be updated."] = "Yeniden adlandırılmış workflow’lar birden fazla Invoke Workflow File referansının güncellenmesini gerektirebilir.",
        ["Verify the referenced workflow exists."] = "Referans verilen workflow’un mevcut olduğunu doğrulayın.",
        ["Confirm the suggested path is the intended target before editing in UiPath Studio."] = "UiPath Studio’da düzenlemeden önce önerilen path’in istenen target olduğunu doğrulayın.",
        ["Select the intended workflow file."] = "Amaçlanan workflow dosyasını seçin.",
        ["Review the finding in UiPath Studio."] = "Bulguyu UiPath Studio içinde gözden geçirin.",
        ["Make the smallest safe manual change."] = "En küçük güvenli manuel değişikliği yapın.",
        ["Re-run analysis after the change."] = "Değişiklikten sonra analizi yeniden çalıştırın.",
        ["This recommendation may affect workflow behavior and should be reviewed manually."] = "Bu öneri workflow behavior’ı etkileyebilir ve manuel olarak gözden geçirilmelidir.",
        ["Review the affected workflow in UiPath Studio."] = "Etkilenen workflow’u UiPath Studio içinde gözden geçirin.",
        ["Re-run analysis after making the manual change."] = "Manuel değişiklikten sonra analizi yeniden çalıştırın.",
        ["Confirm the intended behavior before editing the workflow."] = "Workflow’u düzenlemeden önce amaçlanan behavior’ı doğrulayın.",
        ["Review the proposed DisplayName."] = "Önerilen DisplayName değerini gözden geçirin.",
        ["Apply the DisplayName change in UiPath Studio or through the Safe Apply flow."] = "DisplayName değişikliğini UiPath Studio içinde veya Safe Apply flow üzerinden uygulayın.",
        ["Re-run analysis to confirm the finding is resolved."] = "Bulgunun çözüldüğünü doğrulamak için analizi yeniden çalıştırın.",
        ["Low risk: DisplayName is designer metadata and should not affect runtime behavior."] = "Düşük risk: DisplayName designer metadata’dır ve runtime behavior’ı etkilememelidir.",
        ["Review the suggested name in context before changing it in UiPath Studio."] = "UiPath Studio’da değiştirmeden önce önerilen adı context içinde gözden geçirin.",
        ["Low risk when performed manually: DisplayName is designer metadata and should not change runtime behavior."] = "Manuel yapıldığında düşük risk: DisplayName designer metadata’dır ve runtime behavior’ı değiştirmemelidir.",
        ["Auto-apply is only available when a single affected activity is selected with a stable locator."] = "Auto-apply yalnız stable locator ile tek bir affected activity seçildiğinde kullanılabilir.",
        ["Instruction-only preview. No XAML patch is generated."] = "Sadece talimat önizlemesi. XAML patch üretilmez.",
        ["Review and apply manually in UiPath Studio."] = "UiPath Studio içinde manuel olarak gözden geçirip uygulayın."
    };

    private readonly IRpaDevAssistantLocalizer localizer;

    public UiPathFixSuggestionLocalizer(IRpaDevAssistantLocalizer localizer)
    {
        this.localizer = localizer;
    }

    public UiPathFixSuggestion Localize(UiPathFixSuggestion suggestion, string? locale)
    {
        return suggestion with
        {
            Title = localizer.Get($"Fixes.{suggestion.RuleId}.Title", locale, fallback: suggestion.Title),
            Description = localizer.Get($"Fixes.{suggestion.RuleId}.Description", locale, fallback: suggestion.Description),
            Explanation = localizer.Get($"Fixes.{suggestion.RuleId}.Explanation", locale, fallback: suggestion.Explanation),
            Steps = LocalizeList(suggestion.Steps, locale),
            Risks = LocalizeList(suggestion.Risks, locale),
            ValidationNotes = LocalizeList(suggestion.ValidationNotes, locale),
            UserInputHints = LocalizeList(suggestion.UserInputHints, locale),
            PatchPreview = suggestion.PatchPreview is null ? null : suggestion.PatchPreview with
            {
                Description = LocalizeText(suggestion.PatchPreview.Description, locale),
                Notes = LocalizeList(suggestion.PatchPreview.Notes, locale)
            }
        };
    }

    private static IReadOnlyList<string> LocalizeList(IReadOnlyList<string> values, string? locale)
    {
        if (!string.Equals(locale, SupportedLocale.Turkish, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(locale, "tr-TR", StringComparison.OrdinalIgnoreCase))
        {
            return values;
        }

        return values.Select(value => TurkishFixText.TryGetValue(value, out var translated) ? translated : value).ToArray();
    }

    private static string? LocalizeText(string? value, string? locale)
    {
        if (string.IsNullOrWhiteSpace(value)
            || (!string.Equals(locale, SupportedLocale.Turkish, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(locale, "tr-TR", StringComparison.OrdinalIgnoreCase)))
        {
            return value;
        }

        return TurkishFixText.TryGetValue(value, out var translated) ? translated : value;
    }
}
