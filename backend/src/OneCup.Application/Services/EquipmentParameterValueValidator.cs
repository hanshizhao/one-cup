using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 设备模板参数值校验器（纯函数，无副作用，便于单测）。
/// 提供两种模式：
/// - ValidateValue: 强校验（保存时用），失败抛 DomainException
/// - EvaluateStatus: 读时状态判定（valid/invalid/orphan），不抛异常
/// 对应 spec §3.2/3.3。
/// </summary>
public static class EquipmentParameterValueValidator
{
    /// <summary>
    /// 强校验单个值（保存时用）。失败抛 DomainException。
    /// </summary>
    /// <param name="param">参数定义；null 表示参数已删除（孤儿），应调用方先过滤</param>
    /// <param name="value">提交的值</param>
    public static void ValidateValue(EquipmentTypeParameter? param, string? value)
    {
        if (param is null)
        {
            throw new DomainException("参数定义不存在，请清除该值");
        }

        // 必填校验
        if (param.Required && string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"参数「{param.Name}」为必填项");
        }

        // 空值且非必填 → 通过
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        switch (param.ValueType)
        {
            case ParameterValueType.Number:
                ValidateNumber(param, value);
                break;
            case ParameterValueType.Enum:
                ValidateEnum(param, value);
                break;
            case ParameterValueType.Text:
                // 文本类型无额外约束（长度由 DTO 校验器兜底）
                break;
        }
    }

    /// <summary>
    /// 读时状态判定（不抛异常）。用于详情/列表返回 status 字段。
    /// </summary>
    /// <param name="param">参数定义；null 表示孤儿（参数已删除）</param>
    /// <param name="value">当前值</param>
    /// <returns>(status, statusMessage)，status: valid/invalid/orphan</returns>
    public static (string Status, string? Message) EvaluateStatus(EquipmentTypeParameter? param, string? value)
    {
        // 孤儿：参数定义已删除
        if (param is null)
        {
            return ("orphan", "参数已删除，请清除该值");
        }

        // 必填且空
        if (param.Required && string.IsNullOrWhiteSpace(value))
        {
            return ("invalid", $"参数「{param.Name}」为必填项");
        }

        // 空值且非必填 → 有效
        if (string.IsNullOrWhiteSpace(value))
        {
            return ("valid", null);
        }

        return param.ValueType switch
        {
            ParameterValueType.Number => EvaluateNumber(param, value),
            ParameterValueType.Enum   => EvaluateEnum(param, value),
            ParameterValueType.Text   => ("valid", null),
            _ => ("valid", null),
        };
    }

    // ── 数值校验 ──

    private static void ValidateNumber(EquipmentTypeParameter param, string value)
    {
        if (!decimal.TryParse(value, out var num))
        {
            throw new DomainException($"参数「{param.Name}」的值「{value}」不是有效数值");
        }

        if (decimal.TryParse(param.MinValue, out var min) && num < min)
        {
            throw new DomainException($"参数「{param.Name}」的值「{value}」低于最小值 {min}");
        }

        if (decimal.TryParse(param.MaxValue, out var max) && num > max)
        {
            throw new DomainException($"参数「{param.Name}」的值「{value}」超出最大值 {max}");
        }

        if (param.Precision is int precision && precision >= 0)
        {
            var decimals = value.Contains('.') ? value.SkipWhile(c => c != '.').Skip(1).Count() : 0;
            if (decimals > precision)
            {
                throw new DomainException($"参数「{param.Name}」的值「{value}」小数位超过 {precision} 位");
            }
        }
    }

    private static (string, string?) EvaluateNumber(EquipmentTypeParameter param, string value)
    {
        if (!decimal.TryParse(value, out var num))
            return ("invalid", $"参数「{param.Name}」的值「{value}」不是有效数值");

        if (decimal.TryParse(param.MinValue, out var min) && num < min)
            return ("invalid", $"参数「{param.Name}」的值「{value}」低于最小值 {min}");

        if (decimal.TryParse(param.MaxValue, out var max) && num > max)
            return ("invalid", $"参数「{param.Name}」的值「{value}」超出最大值 {max}");

        if (param.Precision is int precision && precision >= 0)
        {
            var decimals = value.Contains('.') ? value.SkipWhile(c => c != '.').Skip(1).Count() : 0;
            if (decimals > precision)
                return ("invalid", $"参数「{param.Name}」的值「{value}」小数位超过 {precision} 位");
        }

        return ("valid", null);
    }

    // ── 枚举校验 ──

    private static void ValidateEnum(EquipmentTypeParameter param, string value)
    {
        var options = ParseOptions(param.Options);
        if (options.Count == 0)
        {
            throw new DomainException($"参数「{param.Name}」未配置可选值");
        }
        if (!options.Contains(value))
        {
            throw new DomainException($"参数「{param.Name}」的值「{value}」不在可选值列表内");
        }
    }

    private static (string, string?) EvaluateEnum(EquipmentTypeParameter param, string value)
    {
        var options = ParseOptions(param.Options);
        if (options.Count == 0 || !options.Contains(value))
            return ("invalid", $"参数「{param.Name}」的值「{value}」不是有效选项");
        return ("valid", null);
    }

    /// <summary>解析 Options JSON 数组字符串为列表。</summary>
    public static List<string> ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
            return new();

        // 简单解析：["a","b","c"] → ["a","b","c"]
        // 用 System.Text.Json 反序列化
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(optionsJson) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>序列化选项列表为 JSON 字符串（实体存储用）。</summary>
    public static string? SerializeOptions(List<string>? options)
    {
        if (options is null || options.Count == 0)
            return null;
        return System.Text.Json.JsonSerializer.Serialize(options);
    }
}
