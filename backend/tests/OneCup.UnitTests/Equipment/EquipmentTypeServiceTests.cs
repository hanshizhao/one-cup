using OneCup.Application.Dtos.System;
using OneCup.Application.Services;
using OneCup.Application.Validators.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using Xunit;

namespace OneCup.UnitTests.Equipment;

public class EquipmentTypeServiceTests
{
    private static (OneCupDbContext db, EquipmentTypeService svc, FakeNumberingService numbering) Setup()
    {
        var db = EquipmentTestHelper.CreateDb("eqtype");
        var numbering = new FakeNumberingService("EQT-");
        var svc = new EquipmentTypeService(
            new Repository<EquipmentType>(db),
            new Repository<Domain.Entities.Equipment>(db),
            new Repository<Domain.Entities.Process>(db),
            new UnitOfWork(db),
            numbering,
            new CreateEquipmentTypeRequestValidator(),
            new UpdateEquipmentTypeRequestValidator());
        return (db, svc, numbering);
    }

    private static CreateEquipmentTypeRequest ValidCreate() => new()
    {
        Name = "定型机",
        Parameters = new()
        {
            new() { Name = "车速", ValueType = ParameterValueType.Number, MinValue = "80", MaxValue = "200", Required = true, SortOrder = 1 },
            new() { Name = "档位", ValueType = ParameterValueType.Enum, Options = new() { "低", "中", "高" }, Required = true, SortOrder = 2 },
        },
    };

    [Fact]
    public async Task CreateAsync_GeneratesCode_AndPersistsParameters()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "EQT-0001";

        var dto = await svc.CreateAsync(ValidCreate());

        Assert.Equal("EQT-0001", dto.Code);
        Assert.Equal("定型机", dto.Name);
        Assert.Equal(2, dto.Parameters.Count);
        Assert.Equal("车速", dto.Parameters[0].Name);
        Assert.Equal(ParameterValueType.Number, dto.Parameters[0].ValueType);
        Assert.Equal("80", dto.Parameters[0].MinValue);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate());

        await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(ValidCreate()));
    }

    [Fact]
    public async Task UpdateAsync_SyncsParameters_AddUpdateDelete()
    {
        var (_, svc, _) = Setup();
        var created = await svc.CreateAsync(ValidCreate());

        // 删除"档位"，更新"车速"范围，新增"温度"
        var update = new UpdateEquipmentTypeRequest
        {
            Name = "定型机",
            Parameters = new()
            {
                new() { Id = created.Parameters[0].Id, Name = "车速", ValueType = ParameterValueType.Number, MinValue = "50", MaxValue = "150", Required = true, SortOrder = 1 },
                new() { Name = "温度", ValueType = ParameterValueType.Number, MinValue = "100", MaxValue = "220", Required = true, SortOrder = 2 },
            },
        };

        var updated = await svc.UpdateAsync(created.Id, update);

        Assert.Equal(2, updated.Parameters.Count);
        Assert.Equal("温度", updated.Parameters[1].Name);
        Assert.DoesNotContain(updated.Parameters, p => p.Name == "档位");
        Assert.Equal("50", updated.Parameters[0].MinValue);  // 更新生效
    }

    [Fact]
    public async Task DeleteAsync_WithEquipment_Throws()
    {
        var (db, svc, _) = Setup();
        var created = await svc.CreateAsync(ValidCreate());

        // 手动塞一台设备引用此类型
        db.Equipments.Add(new Domain.Entities.Equipment { Code = "EQ-001", Name = "1号机", EquipmentTypeId = created.Id });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => svc.DeleteAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_NoReferences_Succeeds()
    {
        var (_, svc, _) = Setup();
        var created = await svc.CreateAsync(ValidCreate());

        await svc.DeleteAsync(created.Id);

        var found = await svc.GetByIdAsync(created.Id);
        Assert.Null(found);
    }
}
