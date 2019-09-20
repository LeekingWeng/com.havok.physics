using Unity.Entities;
using Unity.Jobs;
using Unity.Physics;
using Unity.Physics.Systems;

namespace Havok.Physics.Authoring
{
    // Implementations of debug display systems for when Havok is present.
    // These call the Havok implementation of e.g. IContactsJob.Schedule() instead of the Unity implementations.
    //
    // Note, the identically named Unity.Physics systems will still be present in the scene,
    // but will have no effect when Havok does the simulation step.

    [UpdateBefore(typeof(StepPhysicsWorld))]
    public class DisplayContactsSystem : Unity.Physics.Authoring.DisplayContactsSystem
    {
        protected override JobHandle ScheduleContactsJob(DisplayContactsJob job, ISimulation simulation, ref PhysicsWorld world, JobHandle inDeps)
        {
            return job.Schedule(simulation, ref world, inDeps);
        }
    }

    [UpdateAfter(typeof(StepPhysicsWorld)), UpdateBefore(typeof(EndFramePhysicsSystem))]
    public class DisplayCollisionEventsSystem : Unity.Physics.Authoring.DisplayCollisionEventsSystem
    {
        protected override JobHandle ScheduleCollisionEventsJob(DisplayCollisionEventsJob job, ISimulation simulation, ref PhysicsWorld world, JobHandle inDeps)
        {
            return job.Schedule(simulation, ref world, inDeps);
        }
    }

    [UpdateAfter(typeof(StepPhysicsWorld)), UpdateBefore(typeof(EndFramePhysicsSystem))]
    public class DisplayTriggerEventsSystem : Unity.Physics.Authoring.DisplayTriggerEventsSystem
    {
        protected override JobHandle ScheduleTriggerEventsJob(DisplayTriggerEventsJob job, ISimulation simulation, ref PhysicsWorld world, JobHandle inDeps)
        {
            return job.Schedule(simulation, ref world, inDeps);
        }
    }
}
