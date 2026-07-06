using OneCup.Application.Dtos.System;
using OneCup.Application.Services;
using OneCup.Application.Validators.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using Xunit;

namespace OneCup.UnitTests.Equipment;

public class EquipmentTemplateServiceTests
{
    private static (OneCupDbContext db, EquipmentTypeService typeSvc, EquipmentTemplateService tplSvc) Setup()
    {
        var db = EquipmentTestHelper.CreateDb("eqtpl");
        var numbering = new FakeNumberingService("EQT-");
        var typeSvc = new EquipmentTypeService(
            new Repository<EquipmentType>(db),
            new Repository<Domain.Entities.Equipment>(db),
            new UnitOfWork(db),
            numbering,
            new CreateEquipmentTypeRequestValidator(),
            new UpdateEquipmentTypeRequestValidator());
        var tplSvc = new EquipmentTemplateService(
            new Repository<EquipmentTemplate>(db),
            new Repository<EquipmentType>(db),
            new Repository<Domain.Entities.Process>(db),
            new UnitOfWork(db),
            new CreateEquipmentTemplateRequestValidator(),
            new UpdateEquipmentTemplateRequestValidator());
        return (db, typeSvc, tplSvc);
    }

    private static async Task<(Guid typeId, Guid numberParamId, Guid enumParamId)> SeedType(EquipmentTypeService typeSvc)
    {
        var dto = await typeSvc.CreateAsync(new CreateEquipmentTypeRequest
        {
            Name = "定型机",
            Parameters = new()
            {
                new() { Name = "车速", ValueType = ParameterValueType.Number, MinValue = "80", MaxValue = "200", Required = true, SortOrder = 1 },
                new() { Name = "档位", ValueType = ParameterValueType.Enum, Options = new() { "低", "中", "高" }, Required = true, SortOrder = 2 },
            },
        });
        return (dto.Id, dto.Parameters[0].Id, dto.Parameters[1].Id);
    }

    private static async Task<Guid> SeedProcess(OneCupDbContext db)
    {
        var p = new Domain.Entities.Process { Code = "PRC-001", Name = "定型" };
        db.Processes.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task CreateAsync_ValidValues_Succeeds()
    {
        var (db, typeSvc, tplSvc) = Setup();
        var (typeId, numParamId, enumParamId) = await SeedType(typeSvc);
        var processId = await SeedProcess(db);

        var dto = await tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "高温快烤",
            ProcessId = processId,
            Values = new()
            {
                new() { ParameterId = numParamId, Value = "150" },
                new() { ParameterId = enumParamId, Value = "高" },
            },
        });

        Assert.Equal("高温快烤", dto.Name);
        Assert.Equal(2, dto.Values.Count);
        Assert.All(dto.Values, v => Assert.Equal("valid", v.Status));
    }

    [Fact]
    public async Task CreateAsync_NumberOutOfRange_Throws()
    {
        var (db, typeSvc, tplSvc) = Setup();
        var (typeId, numParamId, _) = await SeedType(typeSvc);
        var processId = await SeedProcess(db);

        await Assert.ThrowsAsync<DomainException>(() => tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "超限",
            ProcessId = processId,
            Values = new() { new() { ParameterId = numParamId, Value = "250" } },
        }));
    }

    [Fact]
    public async Task CreateAsync_EnumNotInOptions_Throws()
    {
        var (db, typeSvc, tplSvc) = Setup();
        var (typeId, _, enumParamId) = await SeedType(typeSvc);
        var processId = await SeedProcess(db);

        await Assert.ThrowsAsync<DomainException>(() => tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "非法枚举",
            ProcessId = processId,
            Values = new() { new() { ParameterId = enumParamId, Value = "极高" } },
        }));
    }

    [Fact]
    public async Task CreateAsync_RequiredEmpty_Throws()
    {
        var (db, typeSvc, tplSvc) = Setup();
        var (typeId, numParamId, _) = await SeedType(typeSvc);
        var processId = await SeedProcess(db);

        await Assert.ThrowsAsync<DomainException>(() => tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "缺值",
            ProcessId = processId,
            Values = new() { new() { ParameterId = numParamId, Value = "" } },
        }));
    }

    [Fact]
    public async Task GetById_OrphanValue_ReturnsOrphanStatus()
    {
        var (db, typeSvc, tplSvc) = Setup();
        var (typeId, numParamId, _) = await SeedType(typeSvc);
        var processId = await SeedProcess(db);

        // 建模板
        var created = await tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "T1", ProcessId = processId,
            Values = new() { new() { ParameterId = numParamId, Value = "100" } },
        });

        // 删除参数定义（通过更新类型，不传该参数）
        await typeSvc.UpdateAsync(typeId, new UpdateEquipmentTypeRequest
        {
            Name = "定型机",
            Parameters = new() { },  // 清空参数
        });

        // 读取模板 → 该值应标 orphan
        var detail = await tplSvc.GetByIdAsync(typeId, created.Id);
        Assert.NotNull(detail);
        Assert.Equal("orphan", detail!.Values[0].Status);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateNameSameProcess_Throws()
    {
        var (db, typeSvc, tplSvc) = Setup();
        var (typeId, numParamId, _) = await SeedType(typeSvc);
        var processId = await SeedProcess(db);

        await tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "T1", ProcessId = processId,
            Values = new() { new() { ParameterId = numParamId, Value = "100" } },
        });

        await Assert.ThrowsAsync<DomainException>(() => tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "T1", ProcessId = processId,
            Values = new() { new() { ParameterId = numParamId, Value = "100" } },
        }));
    }
}
