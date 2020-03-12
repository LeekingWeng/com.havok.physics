using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;

namespace Unity.Physics
{
    // Interface for jobs that iterate through the list of collision events produced by the solver.
    [JobProducerType(typeof(IHavokCollisionEventsJobExtensions.CollisionEventJobProcess<>))]
    public interface ICollisionEventsJob : ICollisionEventsJobBase
    {
    }

    public static class IHavokCollisionEventsJobExtensions
    {
        // Schedule() implementation for ICollisionEventsJob when Havok Physics is available
        public static unsafe JobHandle Schedule<T>(this T jobData, ISimulation simulation, ref PhysicsWorld world, JobHandle inputDeps)
            where T : struct, ICollisionEventsJobBase
        {
            switch (simulation.Type)
            {
                case SimulationType.UnityPhysics:
                    // Call the scheduling method for Unity.Physics
                    return ICollisionEventJobExtensions.ScheduleUnityPhysicsCollisionEventsJob(jobData, simulation, ref world, inputDeps);

                case SimulationType.HavokPhysics:
                {
                    var data = new CollisionEventJobData<T>
                    {
                        UserJobData = jobData,
                        EventReader = ((Havok.Physics.HavokSimulation)simulation).CollisionEvents
                    };

                    // Ensure the input dependencies include the end-of-simulation job, so events will have been generated
                    inputDeps = JobHandle.CombineDependencies(inputDeps, simulation.FinalSimulationJobHandle);

                    var parameters = new JobsUtility.JobScheduleParameters(
                        UnsafeUtility.AddressOf(ref data),
                        CollisionEventJobProcess<T>.Initialize(), inputDeps, ScheduleMode.Batched);
                    return JobsUtility.Schedule(ref parameters);
                }

                default:
                    return inputDeps;
            }
        }

        internal unsafe struct CollisionEventJobData<T> where T : struct
        {
            public T UserJobData;
            public HavokCollisionEvents EventReader;
        }

        internal struct CollisionEventJobProcess<T> where T : struct, ICollisionEventsJobBase
        {
            static IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                {
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(CollisionEventJobData<T>),
                        typeof(T), JobType.Single, (ExecuteJobFunction)Execute);
                }
                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref CollisionEventJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges jobRanges, int jobIndex);

            public unsafe static void Execute(ref CollisionEventJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges jobRanges, int jobIndex)
            {
                foreach (var collisionEvent in jobData.EventReader)
                {
                    jobData.UserJobData.Execute(collisionEvent);
                }
            }
        }
    }
}
