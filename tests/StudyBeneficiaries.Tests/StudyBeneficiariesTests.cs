using System.Reflection;
using API.Controllers;
using Application.Common;
using Application.DTOs.Request;
using Application.Features.CentralStudies.Lookups;
using Application.Features.CentralStudies.Lookups.Catalogs.Commands.Create;
using Application.Features.CentralStudies.Lookups.Catalogs.Commands.Delete;
using Application.Features.CentralStudies.Lookups.Catalogs.Commands.Update;
using Application.Features.CentralStudies.Lookups.Catalogs.Queries.GetAll;
using Application.Features.CentralStudies.Requests;
using Application.Helper.Profiles;
using AutoMapper;
using Domain.Entities.CentralStudiesBranch;
using Infrastructure.Persistence.Data;
using Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace StudyBeneficiaries.Tests;

public class StudyBeneficiariesTests : IDisposable
{
    private readonly DataContext _db = new(new DbContextOptionsBuilder<DataContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private readonly IMapper _mapper = new MapperConfiguration(c => c.AddProfile<CentralStudiesProfile>(), NullLoggerFactory.Instance).CreateMapper();
    private StudyLookupService Lookups => new(_db);
    private StudyRequestSupport Support => new(null!, null!, null!, Lookups);

    private async Task<StudyBeneficiary> Add(string name = "جهة مستفيدة", bool active = true, string? code = "001")
    {
        var beneficiary = new StudyBeneficiary { Name = name, Code = code, IsActive = active };
        await Lookups.AddAsync(beneficiary);
        return beneficiary;
    }

    [Fact]
    public async Task Request_uses_the_catalog_name_instead_of_client_supplied_text()
    {
        var beneficiary = await Add();
        var request = _mapper.Map<StudyRequest>(new StudyRequestFormDto
        {
            MainBeneficiaryId = beneficiary.Id, MainBeneficiary = "اسم مرسل من العميل", EntityCode = "FORGED"
        });
        Assert.Null(request.EntityCode);
        var selected = await Support.BeneficiaryAsync(request.MainBeneficiaryId);
        request.MainBeneficiary = selected.Name;
        request.EntityCode = selected.Code;
        Assert.Equal(beneficiary.Id, request.MainBeneficiaryId);
        Assert.Equal("جهة مستفيدة", request.MainBeneficiary);
        Assert.Equal("001", request.EntityCode);
    }

    [Fact]
    public async Task Inactive_beneficiaries_cannot_be_selected_for_new_or_different_requests()
    {
        var inactive = await Add(active: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Support.BeneficiaryAsync(inactive.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Support.BeneficiaryAsync(inactive.Id,
            new StudyRequest { MainBeneficiaryId = 999, MainBeneficiary = "جهة أخرى" }));
    }

    [Fact]
    public async Task Editing_can_keep_an_inactive_or_renamed_beneficiary_without_rewriting_the_document()
    {
        var beneficiary = await Add("اسم جديد", active: false);
        var current = new StudyRequest { MainBeneficiaryId = beneficiary.Id, MainBeneficiary = "الاسم في المستند" };
        var selected = await Support.BeneficiaryAsync(beneficiary.Id, current);
        Assert.Equal("الاسم في المستند", selected.Name);
        Assert.Equal("001", selected.Code);
        _mapper.Map(new StudyRequestFormDto { MainBeneficiaryId = beneficiary.Id, MainBeneficiary = "نص مزيف" }, current);
        Assert.Equal("الاسم في المستند", current.MainBeneficiary);
    }

    [Fact]
    public async Task Missing_and_unknown_selections_are_rejected()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => Support.BeneficiaryAsync(null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Support.BeneficiaryAsync(0));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => Support.BeneficiaryAsync(999));
    }

    [Fact]
    public async Task Catalog_supports_create_update_active_filter_and_duplicate_name_rejection()
    {
        var create = new CreateStudyLookupCommandHandler(Lookups, _mapper);
        var dto = await create.Handle(new(StudyLookupKind.Beneficiary, new() { Name = " جهة أولى ", Code = " 002 " }), default);
        Assert.Equal("جهة أولى", dto.Name);
        Assert.Equal("002", dto.Code);
        await Assert.ThrowsAsync<InvalidOperationException>(() => create.Handle(
            new(StudyLookupKind.Beneficiary, new() { Name = dto.Name }), default));
        var other = await Add("جهة ثانية");
        var update = new UpdateStudyLookupCommandHandler(Lookups, _mapper);
        await Assert.ThrowsAsync<InvalidOperationException>(() => update.Handle(
            new(StudyLookupKind.Beneficiary, dto.Id, new() { Name = other.Name }), default));
        var renamed = await update.Handle(new(StudyLookupKind.Beneficiary, dto.Id,
            new() { Name = "جهة معطلة", Code = "002", IsActive = false }), default);
        Assert.False(renamed.IsActive);
        var query = new GetStudyLookupsQueryHandler(Lookups, _mapper);
        Assert.Equal(2, (await query.Handle(new(StudyLookupKind.Beneficiary, false), default)).Count);
        var active = Assert.Single(await query.Handle(new(StudyLookupKind.Beneficiary, true), default));
        Assert.Equal(other.Id, active.Id);
    }

    [Fact]
    public async Task Used_beneficiaries_cannot_be_deleted_even_after_their_name_changes()
    {
        var beneficiary = await Add();
        _db.StudyRequests.Add(new StudyRequest { MainBeneficiaryId = beneficiary.Id, MainBeneficiary = beneficiary.Name });
        await _db.SaveChangesAsync();
        beneficiary.Name = "اسم معدل";
        await _db.SaveChangesAsync();
        var delete = new DeleteStudyLookupCommandHandler(Lookups);
        await Assert.ThrowsAsync<InvalidOperationException>(() => delete.Handle(new(StudyLookupKind.Beneficiary, beneficiary.Id), default));
        Assert.NotNull(await Lookups.GetBeneficiaryAsync(beneficiary.Id));
    }

    [Fact]
    public async Task Unused_beneficiaries_can_be_deleted()
    {
        var beneficiary = await Add();
        await new DeleteStudyLookupCommandHandler(Lookups).Handle(new(StudyLookupKind.Beneficiary, beneficiary.Id), default);
        Assert.Null(await Lookups.GetBeneficiaryAsync(beneficiary.Id));
    }

    [Fact]
    public void Validators_accept_200_character_beneficiaries_but_keep_other_catalog_limits()
    {
        var dto = new StudyLookupFormDto { Name = new string('ج', 200), Code = "001" };
        var create = new CreateStudyLookupCommandValidator();
        Assert.True(create.Validate(new CreateStudyLookupCommand(StudyLookupKind.Beneficiary, dto)).IsValid);
        Assert.False(create.Validate(new CreateStudyLookupCommand(StudyLookupKind.Discipline, dto)).IsValid);
        dto.Name += "ج";
        Assert.False(create.Validate(new CreateStudyLookupCommand(StudyLookupKind.Beneficiary, dto)).IsValid);
        Assert.False(new UpdateStudyLookupCommandValidator().Validate(new UpdateStudyLookupCommand(StudyLookupKind.Beneficiary, 1, dto)).IsValid);
        var requestErrors = new StudyRequestFormValidator().Validate(new StudyRequestFormDto()).Errors;
        Assert.Contains(requestErrors, e => e.PropertyName == nameof(StudyRequestFormDto.MainBeneficiaryId));
    }

    [Fact]
    public void Request_writers_can_read_choices_without_gaining_catalog_management_permissions()
    {
        var policy = typeof(StudyLookupsController).GetMethod(nameof(StudyLookupsController.GetBeneficiaries))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy!;
        // نفس السياسة المسجلة لدى API: لا تعتمد قائمة الطلب على صلاحية فتح الإعدادات.
        Assert.Equal("AnyStudyBeneficiaryPick", policy);
        Assert.Contains("CreateStudyRequest", AppPermissions.AnyOf[policy]);
        Assert.Contains("EditStudyRequest", AppPermissions.AnyOf[policy]);
        Assert.Equal("CreateStudyLookup", typeof(StudyLookupsController).GetMethod(nameof(StudyLookupsController.CreateBeneficiary))!
            .GetCustomAttribute<AuthorizeAttribute>()!.Policy);
    }

    [Fact]
    public void Database_model_restricts_deletion_and_migration_imports_existing_names()
    {
        var entity = _db.Model.FindEntityType(typeof(StudyRequest))!;
        var foreignKey = Assert.Single(entity.GetForeignKeys(), f => f.PrincipalEntityType.ClrType == typeof(StudyBeneficiary));
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
        using var sql = new DataContext(new DbContextOptionsBuilder<DataContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MigrationScriptOnly;Trusted_Connection=True").Options);
        var script = sql.GetService<IMigrator>().GenerateScript("20261110130000_Study_Project_View_Scopes", "20261118065702_Study_Beneficiaries");
        Assert.Contains("INSERT INTO [StudyBeneficiaries]", script);
        Assert.Contains("SELECT DISTINCT LTRIM(RTRIM([MainBeneficiary]))", script);
        Assert.Contains("UPDATE r SET [MainBeneficiaryId] = b.[Id]", script);
        Assert.DoesNotContain("DROP COLUMN [MainBeneficiary]", script);
    }

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567890123456789012345678901")]
    public void Beneficiary_code_is_required_and_limited_to_30_characters(string? code)
    {
        var dto = new StudyLookupFormDto { Name = "جهة", Code = code };
        var create = new CreateStudyLookupCommandValidator().Validate(new CreateStudyLookupCommand(StudyLookupKind.Beneficiary, dto));
        var update = new UpdateStudyLookupCommandValidator().Validate(new UpdateStudyLookupCommand(StudyLookupKind.Beneficiary, 1, dto));
        Assert.Contains(create.Errors, e => e.PropertyName == "Dto.Code");
        Assert.Contains(update.Errors, e => e.PropertyName == "Dto.Code");
        Assert.True(new CreateStudyLookupCommandValidator().Validate(new CreateStudyLookupCommand(StudyLookupKind.Discipline, dto)).IsValid);
    }

    [Fact]
    public async Task Beneficiary_codes_cannot_be_reused_by_another_entity()
    {
        await Add(code: "001");
        var create = new CreateStudyLookupCommandHandler(Lookups, _mapper);
        await Assert.ThrowsAsync<InvalidOperationException>(() => create.Handle(
            new(StudyLookupKind.Beneficiary, new() { Name = "جهة أخرى", Code = " 001 " }), default));
        var other = await Add("جهة أخرى", code: "002");
        var update = new UpdateStudyLookupCommandHandler(Lookups, _mapper);
        await Assert.ThrowsAsync<InvalidOperationException>(() => update.Handle(
            new(StudyLookupKind.Beneficiary, other.Id, new() { Name = other.Name, Code = "001" }), default));
        Assert.Equal("002", other.Code);
    }

    [Fact]
    public async Task Legacy_entities_without_codes_need_completion_before_request_selection()
    {
        var legacy = await Add(code: null);
        Assert.Empty(await Lookups.GetBeneficiariesAsync(true));
        Assert.Single(await Lookups.GetBeneficiariesAsync(false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Support.BeneficiaryAsync(legacy.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Support.BeneficiaryAsync(legacy.Id,
            new StudyRequest { MainBeneficiaryId = legacy.Id, MainBeneficiary = legacy.Name, EntityCode = "OLD" }));
        await new UpdateStudyLookupCommandHandler(Lookups, _mapper).Handle(
            new(StudyLookupKind.Beneficiary, legacy.Id, new() { Name = legacy.Name, Code = "0007" }), default);
        Assert.Single(await Lookups.GetBeneficiariesAsync(true));
        Assert.Equal("0007", (await Support.BeneficiaryAsync(legacy.Id)).Code);
    }

    [Fact]
    public async Task Switching_beneficiary_uses_its_code_even_when_the_request_contains_another_code()
    {
        var selected = await Add(code: "0007");
        var previous = new StudyRequest { MainBeneficiaryId = 999, MainBeneficiary = "جهة سابقة", EntityCode = "999" };
        var resolved = await Support.BeneficiaryAsync(selected.Id, previous);
        Assert.Equal(selected.Name, resolved.Name);
        Assert.Equal("0007", resolved.Code);
    }

    [Fact]
    public void Code_migration_preserves_historical_request_codes_and_imports_only_unambiguous_codes()
    {
        using var sql = new DataContext(new DbContextOptionsBuilder<DataContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=MigrationScriptOnly;Trusted_Connection=True").Options);
        var script = sql.GetService<IMigrator>().GenerateScript("20261118065702_Study_Beneficiaries", "20261118071553_Study_Beneficiary_Codes");
        Assert.Contains("SELECT DISTINCT [MainBeneficiaryId], LTRIM(RTRIM([EntityCode]))", script);
        Assert.Contains("UPDATE b SET [Code] = c.[Code]", script);
        Assert.Contains("CREATE UNIQUE INDEX [IX_StudyBeneficiaries_Code]", script);
        Assert.DoesNotContain("SET [EntityCode]", script);
        var beneficiary = _db.Model.FindEntityType(typeof(StudyBeneficiary))!;
        Assert.Equal(30, beneficiary.FindProperty(nameof(StudyBeneficiary.Code))!.GetMaxLength());
        Assert.Contains(beneficiary.GetIndexes(), i => i.IsUnique && i.Properties.Single().Name == nameof(StudyBeneficiary.Code));
    }
}
