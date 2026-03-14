using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acp.Types;

/// <summary>
/// 与 Python SDK 对齐：按 sessionUpdate 分发到对应类型，未知 discriminator 反序列化为 <see cref="UnknownSessionUpdate"/> 避免报错。
/// </summary>
public sealed class SessionUpdateConverter : JsonConverter<SessionUpdate>
{
    private static readonly Dictionary<string, Type> KnownTypes = new(StringComparer.Ordinal)
    {
        ["user_message_chunk"] = typeof(UserMessageChunk),
        ["agent_message_chunk"] = typeof(AgentMessageChunk),
        ["agent_thought_chunk"] = typeof(AgentThoughtChunk),
        ["tool_call"] = typeof(ToolCallStart),
        ["tool_call_update"] = typeof(ToolCallProgress),
        ["plan"] = typeof(AgentPlanUpdate),
        ["available_commands_update"] = typeof(AvailableCommandsUpdate),
        ["current_mode_update"] = typeof(CurrentModeUpdate),
        ["config_option_update"] = typeof(ConfigOptionUpdate),
        ["session_info_update"] = typeof(SessionInfoUpdate),
        ["usage_update"] = typeof(UsageUpdate),
    };

    public override SessionUpdate? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var discriminator = root.TryGetProperty("sessionUpdate", out var su)
            ? su.GetString()
            : null;

        if (!string.IsNullOrEmpty(discriminator) && KnownTypes.TryGetValue(discriminator, out var knownType))
            return (SessionUpdate?)JsonSerializer.Deserialize(root.GetRawText(), knownType, options);

        return JsonSerializer.Deserialize<UnknownSessionUpdate>(root.GetRawText(), options);
    }

    public override void Write(Utf8JsonWriter writer, SessionUpdate value, JsonSerializerOptions options)
    {
        var type = value.GetType();
        if (type == typeof(UnknownSessionUpdate))
        {
            JsonSerializer.Serialize(writer, value, type, options);
            return;
        }
        foreach (var kv in KnownTypes)
        {
            if (kv.Value == type)
            {
                JsonSerializer.Serialize(writer, value, type, options);
                return;
            }
        }
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
