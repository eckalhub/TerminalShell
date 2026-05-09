using System.Text.Json.Serialization;

namespace TerminalShell.Models;

public sealed class TerminalDraft
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public string PreviewText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                return "[Empty Draft]";
            }

            string normalized = Text
                .Replace("\r\n", " ")
                .Replace('\n', ' ')
                .Replace('\r', ' ')
                .Trim();

            return string.IsNullOrWhiteSpace(normalized) ? "[Empty Draft]" : normalized;
        }
    }

    public TerminalDraft Clone()
    {
        return new TerminalDraft
        {
            Id = Id,
            Text = Text,
            CreatedAtUtc = CreatedAtUtc,
            UpdatedAtUtc = UpdatedAtUtc
        };
    }
}
