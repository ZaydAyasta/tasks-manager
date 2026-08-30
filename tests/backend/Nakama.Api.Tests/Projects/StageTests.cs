using Nakama.Api.BuildingBlocks.Time;
using Nakama.Api.Modules.Projects.Domain;
using Xunit;
namespace Nakama.Api.Tests.Projects;
public sealed class StageTests
{
    private static readonly Guid ProjectId=Guid.NewGuid(); private static readonly DateTimeOffset Now=new(2026,9,1,0,0,0,TimeSpan.Zero);
    [Fact] public void Create_normalizes_name_and_starts_active(){var s=Stage.Create(ProjectId,"  Desarrollo  "," ",1,new Clock(Now));Assert.Equal("Desarrollo",s.Name);Assert.Null(s.Description);Assert.True(s.IsActive);Assert.Equal(1,s.Version);}
    [Fact] public void Create_rejects_invalid_position()=>Assert.Throws<ArgumentException>(()=>Stage.Create(ProjectId,"A",null,0,new Clock(Now)));
    [Fact] public void Update_increments_version_and_timestamp(){var s=Stage.Create(ProjectId,"A",null,1,new Clock(Now));var later=Now.AddMinutes(1);s.UpdateDetails("B",null,new Clock(later));Assert.Equal(2,s.Version);Assert.Equal(later,s.UpdatedAt);}
    [Fact] public void Activate_and_deactivate_enforce_state(){var s=Stage.Create(ProjectId,"A",null,1,new Clock(Now));s.Deactivate(new Clock(Now.AddMinutes(1)));Assert.False(s.IsActive);Assert.Throws<InvalidOperationException>(()=>s.Deactivate(new Clock(Now)));s.Activate(new Clock(Now.AddMinutes(2)));Assert.True(s.IsActive);}
    private sealed class Clock(DateTimeOffset value):IClock{public DateTimeOffset UtcNow=>value;}
}
