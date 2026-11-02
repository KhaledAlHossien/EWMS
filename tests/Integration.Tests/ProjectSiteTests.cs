using System.Net;
using Application.DTOs.Request;
using Application.DTOs.Response;
using Domain.Entities;
using Integration.Tests.Infrastructure;

namespace Integration.Tests;

/// <summary>موقع المشروع مطلوب: يُعرض المشروع على الخريطة تلقائياً بحسب موقعه (كتركيب الأجهزة)، فلا يُنشأ ولا يُعدَّل بلا موقع.</summary>
[Collection(ApiCollection.Name)]
public class ProjectSiteTests(ApiFixture api)
{
    [Fact]
    public async Task A_project_cannot_be_created_or_saved_without_a_site()
    {
        var name = $"مشروع موقع {Guid.NewGuid():N}"[..26];
        var without = await api.Preparer.Post<ProjectResponseDto>("/api/Projects/Create", new ProjectRequestDto { Name = name, Beneficiary = "جهة", DepartmentId = api.DepartmentId });
        Assert.Equal(HttpStatusCode.BadRequest, without.Status);
        Assert.Contains("موقع", without.Raw);

        var project = (await api.Preparer.Post<ProjectResponseDto>("/api/Projects/Create", new ProjectRequestDto { Name = name, Beneficiary = "جهة", DepartmentId = api.DepartmentId, SiteId = api.SiteId })).Ok();
        Assert.Equal(api.SiteId, project.SiteId);

        var cleared = await api.Preparer.Put<ProjectResponseDto>($"/api/Projects/Update/{project.Id}", new ProjectRequestDto
        {
            Name = name, Beneficiary = "جهة", DepartmentId = api.DepartmentId, SiteId = null, RowVersion = project.RowVersion
        });
        Assert.Equal(HttpStatusCode.BadRequest, cleared.Status);          // لا يُفرَّغ الموقع بالتعديل
    }

    [Fact]
    public async Task Editing_a_project_keeps_its_department_and_site_and_a_new_site_replaces_the_old_one()
    {
        var name = $"مشروع تعديل {Guid.NewGuid():N}"[..26];
        var project = (await api.Preparer.Post<ProjectResponseDto>("/api/Projects/Create", new ProjectRequestDto { Name = name, Beneficiary = "جهة", DepartmentId = api.DepartmentId, SiteId = api.SiteId })).Ok();

        // تعديل حقل آخر فقط (المرحلة): القسم والموقع يبقيان في الاستجابة وفي القراءة التالية
        var staged = (await api.Preparer.Put<ProjectResponseDto>($"/api/Projects/Update/{project.Id}", new ProjectRequestDto
        {
            Name = name, Beneficiary = "جهة", DepartmentId = api.DepartmentId, SiteId = api.SiteId, Stage = (int)ProjectStage.Contracting, RowVersion = project.RowVersion
        })).Ok();
        Assert.Equal(api.SiteId, staged.SiteId);
        Assert.Equal(api.DepartmentId, staged.DepartmentId);
        var reread = (await api.Preparer.Get<ProjectResponseDto>($"/api/Projects/Get/{project.Id}")).Ok();
        Assert.Equal(api.SiteId, reread.SiteId);
        Assert.Equal(api.DepartmentId, reread.DepartmentId);

        // موقع آخر: يحلّ محل القديم، واسمه في الاستجابة هو الجديد لا القديم
        var other = (await api.Admin.Post<SiteResponseDto>("/api/Sites/Create", new { name = $"موقع بديل {Guid.NewGuid():N}"[..20], latitude = 34.73, longitude = 36.72 })).Ok();
        var moved = (await api.Preparer.Put<ProjectResponseDto>($"/api/Projects/Update/{project.Id}", new ProjectRequestDto
        {
            Name = name, Beneficiary = "جهة", DepartmentId = api.DepartmentId, SiteId = other.Id, Stage = (int)ProjectStage.Contracting, RowVersion = reread.RowVersion
        })).Ok();
        Assert.Equal(other.Id, moved.SiteId);
        Assert.Equal(other.Name, moved.SiteName);
    }
}
