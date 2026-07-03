using System.Text;
using System.Text.Json;

namespace OneCup.Application.Common;

/// <summary>
/// 请求体脱敏器：把 JSON 中的敏感字段值替换为 "***"。
/// 纯函数，无状态，可单测。用 System.Text.Json 流式改写。
/// </summary>
public static class PayloadSanitizer
{
    /// <summary>请求体最大记录长度（超过则截断标记），避免巨型 body 撑爆 jsonb。</summary>
    private const int MaxPayloadBytes = 8 * 1024;

    /// <summary>敏感字段名黑名单（小写匹配）。</summary>
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "oldPassword", "newPassword",
        "token", "accessToken", "refreshToken",
        "secret", "authorization"
    };

    /// <summary>
    /// 对 JSON 字符串脱敏。返回脱敏后的 JSON 字符串。
    /// 超长返回 "[truncated: {n} bytes]"；非法 JSON 返回 "[binary]"。
    /// </summary>
    public static string? Sanitize(string? json)
    {
        if (json is null) return null;
        if (json.Length == 0) return "";

        if (Encoding.UTF8.GetByteCount(json) > MaxPayloadBytes)
            return $"[truncated: {Encoding.UTF8.GetByteCount(json)} bytes]";

        try
        {
            using var doc = JsonDocument.Parse(json);
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false }))
            {
                WriteValue(writer, doc.RootElement);
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (JsonException)
        {
            return "[binary]";
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var prop in element.EnumerateObject())
                {
                    writer.WritePropertyName(prop.Name);
                    if (SensitiveFields.Contains(prop.Name) && prop.Value.ValueKind == JsonValueKind.String)
                        writer.WriteStringValue("***");
                    else
                        WriteValue(writer, prop.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteValue(writer, item);
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                writer.WriteRawValue(element.GetRawText());
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                writer.WriteRawValue(element.GetRawText());
                break;
        }
    }
}
