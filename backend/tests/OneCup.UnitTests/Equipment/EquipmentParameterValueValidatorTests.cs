using OneCup.Application.Services;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using Xunit;

namespace OneCup.UnitTests.Equipment;

public class EquipmentParameterValueValidatorTests
{
    private static EquipmentTypeParameter Param(ParameterValueType type,
        string? min = null, string? max = null, int? precision = null,
        string? options = null, bool required = false) => new()
    {
        Name = "测试参数",
        ValueType = type,
        MinValue = min,
        MaxValue = max,
        Precision = precision,
        Options = options,
        Required = required,
    };

    // ── 数值校验 ──

    [Fact]
    public void ValidateValue_Number_InRange_Passes()
    {
        var p = Param(ParameterValueType.Number, "80", "200");
        EquipmentParameterValueValidator.ValidateValue(p, "120");
        // 不抛异常即通过
    }

    [Fact]
    public void ValidateValue_Number_AboveMax_Throws()
    {
        var p = Param(ParameterValueType.Number, "80", "200");
        Assert.Throws<DomainException>(() => EquipmentParameterValueValidator.ValidateValue(p, "250"));
    }

    [Fact]
    public void ValidateValue_Number_BelowMin_Throws()
    {
        var p = Param(ParameterValueType.Number, "80", "200");
        Assert.Throws<DomainException>(() => EquipmentParameterValueValidator.ValidateValue(p, "50"));
    }

    [Fact]
    public void ValidateValue_Number_NotNumeric_Throws()
    {
        var p = Param(ParameterValueType.Number);
        Assert.Throws<DomainException>(() => EquipmentParameterValueValidator.ValidateValue(p, "abc"));
    }

    [Fact]
    public void ValidateValue_Number_PrecisionExceeded_Throws()
    {
        var p = Param(ParameterValueType.Number, precision: 1);
        Assert.Throws<DomainException>(() => EquipmentParameterValueValidator.ValidateValue(p, "1.234"));
    }

    [Fact]
    public void ValidateValue_Number_NoRange_AnyNumberPasses()
    {
        var p = Param(ParameterValueType.Number);
        EquipmentParameterValueValidator.ValidateValue(p, "99999");
    }

    // ── 枚举校验 ──

    [Fact]
    public void ValidateValue_Enum_InOptions_Passes()
    {
        var p = Param(ParameterValueType.Enum, options: """["低","中","高"]""");
        EquipmentParameterValueValidator.ValidateValue(p, "中");
    }

    [Fact]
    public void ValidateValue_Enum_NotInOptions_Throws()
    {
        var p = Param(ParameterValueType.Enum, options: """["低","中","高"]""");
        Assert.Throws<DomainException>(() => EquipmentParameterValueValidator.ValidateValue(p, "极高"));
    }

    // ── 通用校验 ──

    [Fact]
    public void ValidateValue_RequiredEmpty_Throws()
    {
        var p = Param(ParameterValueType.Text, required: true);
        Assert.Throws<DomainException>(() => EquipmentParameterValueValidator.ValidateValue(p, ""));
    }

    [Fact]
    public void ValidateValue_NotRequiredEmpty_Passes()
    {
        var p = Param(ParameterValueType.Text, required: false);
        EquipmentParameterValueValidator.ValidateValue(p, "");
    }

    [Fact]
    public void EvaluateStatus_ParamNull_ReturnsOrphan()
    {
        var (status, _) = EquipmentParameterValueValidator.EvaluateStatus(null, "100");
        Assert.Equal("orphan", status);
    }

    [Fact]
    public void EvaluateStatus_NumberOutOfRange_ReturnsInvalid()
    {
        var p = Param(ParameterValueType.Number, "80", "200");
        var (status, _) = EquipmentParameterValueValidator.EvaluateStatus(p, "250");
        Assert.Equal("invalid", status);
    }

    [Fact]
    public void EvaluateStatus_ValidValue_ReturnsValid()
    {
        var p = Param(ParameterValueType.Number, "80", "200");
        var (status, _) = EquipmentParameterValueValidator.EvaluateStatus(p, "120");
        Assert.Equal("valid", status);
    }
}
