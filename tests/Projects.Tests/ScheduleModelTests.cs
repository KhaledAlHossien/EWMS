using Domain.Entities;
using Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Projects.Tests;

/// <summary>نموذج الجدول الزمني في EF (بلا اتصال بقاعدة بيانات): القيود والفهارس التي تحمي القواعد من سباق أو إدخال مباشر</summary>
public class ScheduleModelTests
{
    private static IModel Model()
    {
        using var db = new DataContext(new DbContextOptionsBuilder<DataContext>()
            .UseSqlServer("Server=.;Database=EWMS_never_connected;Trusted_Connection=True").Options);
        return (IModel)db.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType Entity<T>() => Model().FindEntityType(typeof(T))!;

    [Fact]
    public void A_project_has_at_most_one_draft_and_one_approved_schedule()
    {
        var index = Entity<ProjectSchedule>().GetIndexes()
            .Single(i => i.IsUnique && i.GetFilter() != null && i.Properties.Select(p => p.Name).SequenceEqual(["ProjectId", "Status"]));
        Assert.Contains("IN (1, 2)", index.GetFilter());
    }

    [Fact]
    public void Schedule_versions_and_start_orders_are_unique_per_project()
    {
        Assert.Contains(Entity<ProjectSchedule>().GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["ProjectId", "VersionNumber"]));
        Assert.Contains(Entity<ProjectStartOrder>().GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["ProjectId"]));
    }

    [Fact]
    public void Tasks_carry_database_checks_for_dates_progress_and_weight()
    {
        var names = Entity<ScheduleTask>().GetCheckConstraints().Select(c => c.Name).ToList();
        Assert.Contains("CK_ScheduleTasks_Dates", names);
        Assert.Contains("CK_ScheduleTasks_Progress", names);
        Assert.Contains("CK_ScheduleTasks_Weight", names);
        Assert.Contains("CK_ScheduleDependencies_NotSelf", Entity<ScheduleDependency>().GetCheckConstraints().Select(c => c.Name));
    }

    [Fact]
    public void A_dependency_pair_is_unique_and_only_a_draft_deletion_cascades_to_tasks()
    {
        Assert.Contains(Entity<ScheduleDependency>().GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(["PredecessorId", "SuccessorId"]));

        // الجدول يتسلسل إلى بنوده فقط؛ والتبعية والتأخير والتمديد Restrict كي لا يُحذف أثر قرار بحذف بند
        Assert.Equal(DeleteBehavior.Cascade, Entity<ScheduleTask>().GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(ProjectSchedule)).DeleteBehavior);
        foreach (var fk in Entity<ScheduleDependency>().GetForeignKeys()) Assert.Equal(DeleteBehavior.Restrict, fk.DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, Entity<ScheduleDelay>().GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(ScheduleTask)).DeleteBehavior);
        Assert.Equal(DeleteBehavior.Restrict, Entity<ScheduleExtensionItem>().GetForeignKeys().Single(f => f.PrincipalEntityType.ClrType == typeof(ScheduleTask)).DeleteBehavior);
    }

    [Fact]
    public void Dates_are_stored_as_dates_and_the_weight_has_two_decimals()
    {
        var task = Entity<ScheduleTask>();
        foreach (var name in new[] { "PlannedStart", "PlannedEnd", "OriginalStart", "OriginalEnd", "ActualStart", "ActualEnd" })
            Assert.Equal("date", task.FindProperty(name)!.GetColumnType());
        Assert.Equal("decimal(9,2)", task.FindProperty("Weight")!.GetColumnType());
    }
}
