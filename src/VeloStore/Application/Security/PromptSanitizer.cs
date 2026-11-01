namespace VeloStore.Application.Security;

public static class PromptSanitizer
{
    private static readonly string[] BannedChatMlTokens = new[] 
    { 
        "<|system|>", "<|user|>", "<|assistant|>", "<|end|>", 
        "```", "System:", "Instruction:" 
    };

    public static string Sanitize(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput)) return string.Empty;
        
        var safeInput = userInput.Length > 250 ? userInput[..250] : userInput;

        foreach (var token in BannedChatMlTokens)
        {
            safeInput = safeInput.Replace(token, " ", StringComparison.OrdinalIgnoreCase);
        }

        return safeInput.Trim();
    }
}
