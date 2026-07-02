using OneCup.Application.Common;
using OneCup.Domain.Enums;

namespace OneCup.UnitTests.Numbering;

public class PeriodKeyCalculatorTests
{
    // 用北京时间 2026-07-02 14:30 作为测试时间
    private static readonly DateTime Now = new(2026, 7, 2, 14, 30, 0);

    [Fact]
    public void Calc_None_ReturnsEmpty()
    {
        Assert.Equal("", PeriodKeyCalculator.Calc(ResetPeriod.None, Now));
    }

    [Fact]
    public void Calc_Yearly_ReturnsYear()
    {
        Assert.Equal("2026", PeriodKeyCalculator.Calc(ResetPeriod.Yearly, Now));
    }

    [Fact]
    public void Calc_Monthly_ReturnsYearMonth()
    {
        Assert.Equal("202607", PeriodKeyCalculator.Calc(ResetPeriod.Monthly, Now));
    }

    [Fact]
    public void Calc_Daily_ReturnsYearMonthDay()
    {
        Assert.Equal("20260702", PeriodKeyCalculator.Calc(ResetPeriod.Daily, Now));
    }
}
