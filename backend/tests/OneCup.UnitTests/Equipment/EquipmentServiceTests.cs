using OneCup.Application.Dtos.System;
using OneCup.Application.Services;
using OneCup.Application.Validators.System;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using Xunit;
using EquipmentEntity = OneCup.Domain.Entities.Equipment;
using EquipmentType = OneCup.Domain.Entities.EquipmentType;

namespace OneCup.UnitTests.Equipment;

public class EquipmentServiceTests
{
    private static (OneCupDbContext db, EquipmentService svc, FakeNumberingService numbering) Setup()
    {
        var db = EquipmentTestHelper.CreateDb("eq");
        var numbering = new FakeNumberingService("EQ-");
        var svc = new EquipmentService(
            new Repository<EquipmentEntity>(db),
            new Repository<EquipmentType>(db),
            new UnitOfWork(db),
            numbering,
            new CreateEquipmentRequestValidator(),
            new UpdateEquipmentRequestValidator());
        return (db, svc, numbering);
    }

    private static async Task<Guid> SeedType(OneCupDbContext db)
    {
        var t = new EquipmentType { Code = "EQT-001", Name = "定型机" };
        db.EquipmentTypes.Add(t);
        await db.SaveChangesAsync();
        return t.Id;
    }

    private static CreateEquipmentRequest ValidCreate(Guid typeId) => new()
    {
        Name = "1号定型机",
        EquipmentTypeId = typeId,
        Supplier = "某机械厂",
        Location = "1号车间",
        Status = EquipmentStatus.Running,
    };

    [Fact]
    public async Task CreateAsync_GeneratesCode()
    {
        var (db, svc, numbering) = Setup();
        var typeId = await SeedType(db);
        numbering.NextCode = "EQ-0001";

        var dto = await svc.CreateAsync(ValidCreate(typeId));

        Assert.Equal("EQ-0001", dto.Code);
        Assert.Equal("定型机", dto.EquipmentTypeName);
        Assert.Equal(EquipmentStatus.Running, dto.Status);
    }

    [Fact]
    public async Task CreateAsync_NonExistentType_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(
            () => svc.CreateAsync(ValidCreate(Guid.NewGuid())));
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        var (db, svc, _) = Setup();
        var typeId = await SeedType(db);
        await svc.CreateAsync(ValidCreate(typeId));

        await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(ValidCreate(typeId)));
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_AndIdempotent()
    {
        var (db, svc, _) = Setup();
        var typeId = await SeedType(db);
        var created = await svc.CreateAsync(ValidCreate(typeId));

        await svc.DeleteAsync(created.Id);
        // 软删后查不到
        var found = await svc.GetByIdAsync(created.Id);
        Assert.Null(found);

        // 幂等：再删不报错（GetByIdAsync 绕过过滤器）
        await svc.DeleteAsync(created.Id);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields()
    {
        var (db, svc, _) = Setup();
        var typeId = await SeedType(db);
        var created = await svc.CreateAsync(ValidCreate(typeId));

        var updated = await svc.UpdateAsync(created.Id, new UpdateEquipmentRequest
        {
            Name = "1号定型机",
            EquipmentTypeId = typeId,
            Status = EquipmentStatus.Maintenance,
            Location = "2号车间",
        });

        Assert.Equal(EquipmentStatus.Maintenance, updated.Status);
        Assert.Equal("2号车间", updated.Location);
    }
}
