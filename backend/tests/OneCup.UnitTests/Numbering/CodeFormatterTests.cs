using OneCup.Application.Common;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;

namespace OneCup.UnitTests.Numbering;

public class CodeFormatterTests
{
    private static readonly DateTime Now = new(2026, 7, 2, 14, 30, 0);

    [Fact]
    public void Format_AllSegments()
    {
        var code = CodeFormatter.Format("FAB", true, DateSegment.Year, 4, "-", 7, "COT", Now);
        Assert.Equal("FAB-COT-2026-0007", code);
    }

    [Fact]
    public void Format_NoCategory()
    {
        var code = CodeFormatter.Format("FAB", false, DateSegment.Year, 4, "-", 7, "COT", Now);
        Assert.Equal("FAB-2026-0007", code);
    }

    [Fact]
    public void Format_NoDate()
    {
        var code = CodeFormatter.Format("FAB", true, DateSegment.None, 4, "-", 7, "COT", Now);
        Assert.Equal("FAB-COT-0007", code);
    }

    [Fact]
    public void Format_EmptySeparator()
    {
        var code = CodeFormatter.Format("EQ", false, DateSegment.Year, 4, "", 7, null, Now);
        Assert.Equal("EQ20260007", code);
    }

    [Fact]
    public void Format_PrefixAndSeqOnly()
    {
        var code = CodeFormatter.Format("FAB", false, DateSegment.None, 4, "-", 7, null, Now);
        Assert.Equal("FAB-0007", code);
    }

    [Fact]
    public void Format_YearMonth()
    {
        var code = CodeFormatter.Format("CL", true, DateSegment.YearMonth, 3, "-", 7, "RED", Now);
        Assert.Equal("CL-RED-202607-007", code);
    }

    [Fact]
    public void Format_YearMonthDay()
    {
        var code = CodeFormatter.Format("X", false, DateSegment.YearMonthDay, 4, "-", 1, null, Now);
        Assert.Equal("X-20260702-0001", code);
    }

    [Fact]
    public void Format_SeqPadding6()
    {
        var code = CodeFormatter.Format("MAT", false, DateSegment.None, 6, "-", 7, null, Now);
        Assert.Equal("MAT-000007", code);
    }

    [Fact]
    public void Format_SeqOverflow_Throws()
    {
        // seqLength=4 但 seq=10000（5 位）
        Assert.Throws<DomainException>(() =>
            CodeFormatter.Format("FAB", false, DateSegment.None, 4, "-", 10000, null, Now));
    }

    [Fact]
    public void Format_IncludeCategoryButNullCategory_OmitsCategorySegment()
    {
        // 宽容：声明要分类码但传入 null → 该段省略（业务层调用错误应在服务层拦截）
        var code = CodeFormatter.Format("FAB", true, DateSegment.None, 4, "-", 7, null, Now);
        Assert.Equal("FAB-0007", code);
    }

    [Fact]
    public void FormatSample_UsesPlaceholderCategoryAndSeq1()
    {
        var sample = CodeFormatter.FormatSample("FAB", true, DateSegment.Year, 4, "-", Now);
        Assert.Equal("FAB-CAT-2026-0001", sample);
    }
}
