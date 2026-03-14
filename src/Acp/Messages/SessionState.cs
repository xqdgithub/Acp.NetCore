using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Acp.Messages;

/// <summary>
/// 模型信息（与 Python ModelInfo / ACP 一致）
/// </summary>
public class ModelInfo
{
    [JsonPropertyName("modelId")]
    public string ModelId { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Session 模型状态（与 Python SessionModelState 一致）
/// </summary>
public class SessionModelState
{
    [JsonPropertyName("currentModelId")]
    public string CurrentModelId { get; init; } = "";

    [JsonPropertyName("availableModels")]
    public List<ModelInfo> AvailableModels { get; init; } = new();
}

/// <summary>
/// Session 模式（与 Python SessionMode 一致）
/// </summary>
public class SessionMode
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; init; }
}

/// <summary>
/// Session 模式状态（与 Python SessionModeState 一致）
/// </summary>
public class SessionModeState
{
    [JsonPropertyName("currentModeId")]
    public string CurrentModeId { get; init; } = "";

    [JsonPropertyName("availableModes")]
    public List<SessionMode> AvailableModes { get; init; } = new();
}
