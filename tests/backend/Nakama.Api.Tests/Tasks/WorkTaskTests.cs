using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Tasks.Domain;
using DomainTaskStatus = Nakama.Api.Modules.Tasks.Domain.TaskStatus;
using Xunit;
namespace Nakama.Api.Tests.Tasks;
public sealed class WorkTaskTests
{private static readonly DateTimeOffset Now=new(2026,1,1,0,0,0,TimeSpan.Zero);private static readonly Guid Id=Guid.NewGuid();[Fact]public void Creation_normalizes_and_starts_pending(){var t=WorkTask.Create(Id,Id,"  Task  ",null,TaskPriority.High,null,Id,new Clock(Now));Assert.Equal("Task",t.Title);Assert.Equal(DomainTaskStatus.Pending,t.Status);Assert.Equal(1,t.Version);Assert.Null(t.CompletedAt);}[Fact]public void Workflow_completes_only_from_review(){var t=WorkTask.Create(Id,Id,"Task",null,TaskPriority.High,null,Id,new Clock(Now));t.Start(new Clock(Now));t.SubmitForReview(new Clock(Now));t.Complete(new Clock(Now));Assert.Equal(DomainTaskStatus.Completed,t.Status);Assert.Equal(Now,t.CompletedAt);Assert.Throws<InvalidOperationException>(()=>t.Start(new Clock(Now)));}[Fact]public void Changes_increment_version(){var t=WorkTask.Create(Id,Id,"Task",null,TaskPriority.Low,null,Id,new Clock(Now));t.UpdateDetails("Other",null,TaskPriority.Critical,null,new Clock(Now.AddMinutes(1)));Assert.Equal(2,t.Version);Assert.Equal(Now.AddMinutes(1),t.UpdatedAt);}private sealed class Clock(DateTimeOffset x):IClock{public DateTimeOffset UtcNow=>x;}}
