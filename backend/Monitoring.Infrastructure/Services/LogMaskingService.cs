using System.Text.RegularExpressions;
using Monitoring.Core.Interfaces;

namespace Monitoring.Infrastructure.Services;

public partial class LogMaskingService : ILogMaskingService
{
    private const string MaskReplacement = "***MASKED***";

    // Token, password, secret, api key, private key patterns
    [GeneratedRegex(@"(?i)(token|password|secret|api.?key|private.?key)[\s:=]+\S+", RegexOptions.Compiled)]
    private static partial Regex SensitiveKeyValuePattern();

    // Bearer token pattern
    [GeneratedRegex(@"Bearer\s+\S+", RegexOptions.Compiled)]
    private static partial Regex BearerTokenPattern();

    // Basic auth pattern
    [GeneratedRegex(@"Basic\s+\S+", RegexOptions.Compiled)]
    private static partial Regex BasicAuthPattern();

    public string MaskSensitiveData(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;

        var result = SensitiveKeyValuePattern().Replace(message, m =>
        {
            // Preserve the key portion, mask only the value
            var match = m.Value;
            var separatorIndex = FindSeparatorIndex(match);
            if (separatorIndex > 0)
            {
                return match[..separatorIndex] + MaskReplacement;
            }
            return MaskReplacement;
        });

        result = BearerTokenPattern().Replace(result, MaskReplacement);
        result = BasicAuthPattern().Replace(result, MaskReplacement);

        return result;
    }

    private static int FindSeparatorIndex(string value)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] is ':' or '=' or ' ')
            {
                // Skip the separator and any trailing whitespace
                int j = i + 1;
                while (j < value.Length && value[j] == ' ')
                    j++;
                return j;
            }
        }
        return -1;
    }
}
