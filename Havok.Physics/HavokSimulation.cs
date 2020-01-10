using System;
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics;

namespace Havok.Physics
{
    // Registers the Havok simulation as an option in StepPhysicsWorld
    // TODO: Is there a better way to do this registration?
    [UpdateBefore(typeof(Unity.Physics.Systems.StepPhysicsWorld))]
    public class RegisterHavok : ComponentSystem
    {
        protected override void OnUpdate()
        {
            World.GetExistingSystem<Unity.Physics.Systems.StepPhysicsWorld>().RegisterSimulation(SimulationType.HavokPhysics, () =>
            {
                HavokConfiguration config = HasSingleton<HavokConfiguration>() ? GetSingleton<HavokConfiguration>() : HavokConfiguration.Default;
                return new HavokSimulation(config);
            });
        }
    }

    // Steps a physics world using Havok Physics plugin
    public class HavokSimulation : ISimulation
    {
        public SimulationType Type => SimulationType.HavokPhysics;
        public JobHandle FinalSimulationJobHandle { get; protected set; }
        public JobHandle FinalJobHandle { get; protected set; }

        private Storage m_Storage;

        private readonly int m_WorldIndex; //<todo.eoin.multipleWorlds
        private readonly bool m_VisualDebuggerEnabled;
        private readonly unsafe StepContext* m_StepContext;

        internal unsafe HpIntArray* PluginIndexToLocal => m_StepContext->pluginBodyIndexToLocal;
        internal unsafe HpBlockStream* NewBodyPairsStream => m_StepContext->newBroadphasePairs;
        internal unsafe HpBlockStream* ManifoldStream => m_StepContext->manifoldStream;
        internal unsafe HpBlockStream* CollisionEventStream => m_StepContext->collisionEventsStream;
        internal unsafe HpBlockStream* TriggerEventStream => m_StepContext->triggerEventsStream;
        internal unsafe HpGrid* FixedJacobianGrid => m_StepContext->jacFixedGrid;
        internal unsafe HpGrid* MovingJacobianGrid => m_StepContext->jacMovingGrid;
        internal float TimeStep { get; private set; }

        // Copy of MotionVelocity velocities from the start of the step
        internal NativeSlice<Velocity> InputVelocities;

        // Input parameters for HP_StepWorld()
        [StructLayout(LayoutKind.Sequential)]
        internal struct StepInput
        {
            public float m_timeStep;
            public float3 m_gravity;
            public int m_numThreads;
            public int m_numSolverIterations;
        }

        // Context written to by HP_AllocateWorld() and HP_StepWorld()
        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct StepContext
        {
            public Task* broadphase;
            public Task* narrowphase;
            public Task* solverPrep;
            public Task* solverSolve;

            public HpBlockStream* newBroadphasePairs;
            public HpBlockStream* manifoldStream;
            public HpGrid* jacFixedGrid;
            public HpGrid* jacMovingGrid;

            public HpBlockStream* collisionEventsStream;
            public HpBlockStream* triggerEventsStream;

            public HpIntArray* pluginBodyIndexToLocal;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Task
        {
            public IntPtr m_task;
            public int m_multiplicity;
            public int m_runCount;
            public int m_worldIndex;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct Camera
        {
            public float3 From;
            public float3 To;
            public float3 Up;
            public float NearPlane;
            public float FarPlane;
            public float FieldOfView;
        }

        public unsafe HavokSimulation(HavokConfiguration config)
        {
            // Unlock the plugin if it hasn't already been done.
            // If it remains locked, the simulation will do nothing.
            Plugin.EnsureUnlocked();

            m_VisualDebuggerEnabled = config.VisualDebugger.Enable != 0;

            // Allocate this at a fixed memory location. The plugin writes to it.
            m_StepContext = (StepContext*)UnsafeUtility.Malloc(sizeof(StepContext), 16, Allocator.Persistent);
            UnsafeUtility.MemClear(m_StepContext, sizeof(StepContext));

            m_WorldIndex = Plugin.HP_AllocateWorld(ref config, m_StepContext);

            m_Storage = new Storage();
            m_Storage.Initialize();
        }

        public unsafe void Dispose()
        {
            Plugin.HP_DestroyWorld(m_WorldIndex);

            UnsafeUtility.Free(m_StepContext, Allocator.Persistent);

            m_Storage.Dispose();
        }

        public void Step(SimulationStepInput input)
        {
            // TODO : Using the multithreaded version for now, but should do a proper single threaded version
            ScheduleStepJobs(input, new JobHandle());
            FinalJobHandle.Complete();
        }

        public unsafe void ScheduleStepJobs(SimulationStepInput input, JobHandle inputDeps)
        {
            ref PhysicsWorld world = ref input.World;

            m_Storage.InputVelocityCount = world.NumDynamicBodies;
            InputVelocities = m_Storage.InputVelocities;

            // Store the time step so it can be passed to IHavokJacobiansJobExtensions.Schedule()
            TimeStep = input.TimeStep;

            // Early out if there will be no effective result
            if (world.NumDynamicBodies == 0 && !m_VisualDebuggerEnabled)
            {
                FinalSimulationJobHandle = inputDeps;
                FinalJobHandle = inputDeps;
                return;
            }

            // Synchronize the Havok Physics world with the current Unity Physics world
            JobHandle handle = new SyncJob
            {
                World = world,
                WorldIndex = m_WorldIndex
            }.Schedule(inputDeps);

            SimulationCallbacks callbacks = input.Callbacks ?? new SimulationCallbacks();

            // Step the Havok Physics world if necessary
            if (world.NumDynamicBodies > 0)
            {
                // Generate context for this step
                handle = new StartStepJob
                {
                    WorldIndex = m_WorldIndex,
                    StepInput = new StepInput
                    {
                        m_timeStep = input.TimeStep,
                        m_gravity = input.Gravity,
                        m_numSolverIterations = input.NumSolverIterations,
                        m_numThreads = input.ThreadCountHint
                    },
                    StepContext = m_StepContext
                }.Schedule(handle);

                // Broad phase
                if (callbacks.Any(SimulationCallbacks.Phase.PostCreateDispatchPairs))
                {
                    handle = new StepJob(m_StepContext, SimulationCallbacks.Phase.PostCreateDispatchPairs).Schedule(input.ThreadCountHint, 1, handle);
                    handle = callbacks.Execute(SimulationCallbacks.Phase.PostCreateDispatchPairs, this, ref world, handle);
                }

                // Apply gravity and copy input velocities at any point before the end of the step.
                // Note: Havok Physics doesn't "see" this gravity applied, but instead applies it on its own,
                // but we need the velocity with applied gravity for later calculations as input velocity.
                var applyGravityAndCopyInputVelocitiesHandle = Solver.ScheduleApplyGravityAndCopyInputVelocitiesJob(
                    ref world.DynamicsWorld, m_Storage.InputVelocities, input.TimeStep * input.Gravity, handle);

                // Narrow phase
                if (callbacks.Any(SimulationCallbacks.Phase.PostCreateContacts))
                {
                    handle = new StepJob(m_StepContext, SimulationCallbacks.Phase.PostCreateContacts).Schedule(input.ThreadCountHint, 1, handle);
                    handle = callbacks.Execute(SimulationCallbacks.Phase.PostCreateContacts, this, ref world, handle);
                }

                // Create Jacobians
                if (callbacks.Any(SimulationCallbacks.Phase.PostCreateContactJacobians))
                {
                    handle = new StepJob(m_StepContext, SimulationCallbacks.Phase.PostCreateContactJacobians).Schedule(input.ThreadCountHint, 1, handle);
                    handle = callbacks.Execute(SimulationCallbacks.Phase.PostCreateContactJacobians, this, ref world, handle);
                }

                // Solve Jacobians
                handle = new StepJob(m_StepContext, SimulationCallbacks.Phase.PostSolveJacobians).Schedule(input.ThreadCountHint, 1, handle);
                handle = JobHandle.CombineDependencies(handle, applyGravityAndCopyInputVelocitiesHandle);
            }

            // Step the visual debugger server
            // Do this before extracting the motions so that velocities applied by VDB mouse picking are respected
            if (m_VisualDebuggerEnabled)
            {
                var c = UnityEngine.Camera.main;
                Camera camera = (c == null) ? default : new Camera
                {
                    From = c.transform.position,
                    To = c.transform.position + c.transform.forward,
                    Up = c.transform.up,
                    NearPlane = c.nearClipPlane,
                    FarPlane = c.farClipPlane,
                    FieldOfView = c.fieldOfView
                };

                handle = new StepVdbJob
                {
                    WorldIndex = m_WorldIndex,
                    TimeStep = input.TimeStep,
                    Camera = camera
                }.Schedule(handle);
            }

            // Extract the updated motion states from the Havok world
            if (world.NumDynamicBodies > 0)
            {
                handle = callbacks.Execute(SimulationCallbacks.Phase.PostSolveJacobians, this, ref world, handle);
                
                const int syncMotionsBatchSize = 128;
                int numBatches = (world.MotionDatas.Length + syncMotionsBatchSize - 1) / syncMotionsBatchSize;
                handle = new ExtractMotionsJob
                {
                    MotionDatas = world.DynamicsWorld.MotionDatas,
                    MotionVelocities = world.DynamicsWorld.MotionVelocities,
                    WorldIndex = m_WorldIndex,
                    BatchSize = syncMotionsBatchSize
                }.Schedule(numBatches, 1, handle);

                if (input.SynchronizeCollisionWorld)
                {
                    // TODO: timeStep = 0 here for tighter bounds, since it will be rebuilt next step anyway?
                    handle = world.CollisionWorld.ScheduleUpdateDynamicLayer(ref world, input.TimeStep, input.Gravity, input.ThreadCountHint, handle);
                }
            }

            FinalSimulationJobHandle = handle;
            FinalJobHandle = handle;
        }

        internal struct Storage : IDisposable
        {
            private int m_InputVelocityCount;

            private NativeArray<Velocity> m_InputVelocities;

            public NativeSlice<Velocity> InputVelocities => new NativeSlice<Velocity>(m_InputVelocities, 0, m_InputVelocityCount);

            public void Initialize()
            {
                m_InputVelocityCount = 0;
                m_InputVelocities = new NativeArray<Velocity>(0, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            public int InputVelocityCount
            {
                get => m_InputVelocityCount;
                set
                {
                    m_InputVelocityCount = value;
                    if (m_InputVelocities.Length < m_InputVelocityCount)
                    {
                        m_InputVelocities.Dispose();
                        m_InputVelocities = new NativeArray<Velocity>(m_InputVelocityCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    }
                }
            }

            public void Dispose()
            {
                m_InputVelocities.Dispose();
            }
        }

        #region Jobs

#if !UNITY_IOS || UNITY_EDITOR
        [BurstCompile]
#endif
        struct SyncJob : IJob
        {
            public int WorldIndex;
            [ReadOnly] public PhysicsWorld World;

            public unsafe void Execute()
            {
                var bodies = World.Bodies.GetUnsafeReadOnlyPtr();
                var motionDatas = (MotionData*)World.MotionDatas.GetUnsafeReadOnlyPtr();
                var motionVelocities = (MotionVelocity*)World.MotionVelocities.GetUnsafeReadOnlyPtr();
                var joints = (Joint*)World.Joints.GetUnsafeReadOnlyPtr();

                Plugin.HP_SyncWorldIn(
                    WorldIndex,
                    bodies, World.NumBodies, UnsafeUtility.SizeOf<RigidBody>(),
                    motionDatas, World.NumDynamicBodies, sizeof(MotionData),
                    motionVelocities, World.NumDynamicBodies, sizeof(MotionVelocity),
                    joints, World.NumJoints, sizeof(Joint));
            }
        }

#if !UNITY_IOS || UNITY_EDITOR
        [BurstCompile]
#endif
        unsafe struct StartStepJob : IJob
        {
            public int WorldIndex;
            public StepInput StepInput;
            [NativeDisableUnsafePtrRestriction] public StepContext* StepContext;

            public void Execute()
            {
                Plugin.HP_StepWorld(WorldIndex, ref StepInput, StepContext);
            }
        }

#if !UNITY_IOS || UNITY_EDITOR
        [BurstCompile]
#endif
        unsafe struct StepJob : IJobParallelFor
        {
            // TODO : WorldIndex
            public readonly SimulationCallbacks.Phase StopAtPhase;
            [NativeDisableUnsafePtrRestriction] public readonly StepContext* StepContext;

            public StepJob(StepContext* stepContext, SimulationCallbacks.Phase stopAtPhase)
            {
                StepContext = stepContext;
                StopAtPhase = stopAtPhase;
            }

            public void Execute(int i)
            {
                switch (StopAtPhase)
                {
                    case SimulationCallbacks.Phase.PostCreateDispatchPairs:
                        Plugin.HP_ProcessStep(StepContext->broadphase); break;
                    case SimulationCallbacks.Phase.PostCreateContacts:
                        Plugin.HP_ProcessStep(StepContext->narrowphase); break;
                    case SimulationCallbacks.Phase.PostCreateContactJacobians:
                        Plugin.HP_ProcessStep(StepContext->solverPrep); break;
                    case SimulationCallbacks.Phase.PostSolveJacobians:
                    default:
                        Plugin.HP_ProcessStep(StepContext->solverSolve); break;
                }
            }
        }

#if !UNITY_IOS || UNITY_EDITOR
        [BurstCompile]
#endif
        unsafe struct ExtractMotionsJob : IJobParallelFor
        {
            public int WorldIndex;
            public int BatchSize;
            public NativeSlice<MotionData> MotionDatas;
            public NativeSlice<MotionVelocity> MotionVelocities;

            public void Execute(int batchIndex)
            {
                int numMotions = MotionDatas.Length;
                int endIdx = math.min(numMotions, BatchSize * (batchIndex + 1));
                int numInBatch = endIdx - (BatchSize * batchIndex);

                Plugin.HP_SyncMotionsOut(
                    WorldIndex,
                    (MotionData*)MotionDatas.GetUnsafePtr(), numMotions, sizeof(MotionData),
                    (MotionVelocity*)MotionVelocities.GetUnsafePtr(), numMotions, sizeof(MotionVelocity),
                    BatchSize * batchIndex, numInBatch);
            }
        }

#if !UNITY_IOS || UNITY_EDITOR
        [BurstCompile]
#endif
        struct StepVdbJob : IJob
        {
            public int WorldIndex;
            public float TimeStep;
            public Camera Camera;

            public void Execute()
            {
                Plugin.HP_StepVisualDebugger(WorldIndex, TimeStep, ref Camera);
            }
        }

        #endregion

        #region Obsolete
        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("ScheduleStepJobs(SimulationStepInput, JobHandle, out JobHandle, out JobHandle) has been deprecated. Use ScheduleStepJobs(SimulationStepInput, JobHandle) instead. (RemovedAfter 2019-10-15)")]
        public void ScheduleStepJobs(SimulationStepInput input, JobHandle inputDeps, out JobHandle finalSimulationJobHandle, out JobHandle finalJobHandle)
        {
            finalSimulationJobHandle = FinalSimulationJobHandle;
            finalJobHandle = FinalJobHandle;
            ScheduleStepJobs(input, inputDeps);
        }
        #endregion
    }
}
