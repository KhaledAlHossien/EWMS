using System.Net;
using Application.DTOs.Request;
using Application.DTOs.Response;
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
}
