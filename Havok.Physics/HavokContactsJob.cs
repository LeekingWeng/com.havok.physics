using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Unity.Physics
{
    // Interface for jobs that iterate through the list of contact manifolds produced by the narrow phase
    [JobProducerType(typeof(IHavokContactsJobExtensions.ContactsJobProcess<>))]
    public interface IContactsJob : IContactsJobBase
    {
    }

    public static class IHavokContactsJobExtensions
    {
        // Schedule() implementation for IContactsJob when Havok Physics is available
        public static unsafe JobHandle Schedule<T>(this T jobData, ISimulation simulation, ref PhysicsWorld world, JobHandle inputDeps)
            where T : struct, IContactsJobBase
        {
            switch (simulation.Type)
            {
                case SimulationType.UnityPhysics:
                    // Call the scheduling method for Unity.Physics
                    return IContactsJobExtensions.ScheduleUnityPhysicsContactsJob(jobData, simulation, ref world, inputDeps);

                case SimulationType.HavokPhysics:
                {
                    var data = new ContactsJobData<T>
                    {
                        UserJobData = jobData,
                        ManifoldStream = ((Havok.Physics.HavokSimulation)simulation).ManifoldStream,
                        PluginIndexToLocal = ((Havok.Physics.HavokSimulation)simulation).PluginIndexToLocal,
                        Bodies = world.Bodies
                    };
                    var parameters = new JobsUtility.JobScheduleParameters(
                        UnsafeUtility.AddressOf(ref data),
                        ContactsJobProcess<T>.Initialize(), inputDeps, ScheduleMode.Batched);
                    return JobsUtility.Schedule(ref parameters);
                }

                default:
                    return inputDeps;
            }
        }

        internal unsafe struct ContactsJobData<T> where T : struct
        {
            public T UserJobData;
            [NativeDisableUnsafePtrRestriction] public Havok.Physics.HpBlockStream* ManifoldStream;
            [NativeDisableUnsafePtrRestriction] public Havok.Physics.HpIntArray* PluginIndexToLocal;
            // Disable aliasing restriction in case T has a NativeArray of PhysicsWorld.Bodies
            [ReadOnly, NativeDisableContainerSafetyRestriction] public NativeArray<RigidBody> Bodies;
        }

        internal struct ContactsJobProcess<T> where T : struct, IContactsJobBase
        {
            static IntPtr jobReflectionData;

            public static IntPtr Initialize()
            {
                if (jobReflectionData == IntPtr.Zero)
                {
                    jobReflectionData = JobsUtility.CreateJobReflectionData(typeof(ContactsJobData<T>),
                        typeof(T), JobType.Single, (ExecuteJobFunction)Execute);
                }
                return jobReflectionData;
            }

            public delegate void ExecuteJobFunction(ref ContactsJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges jobRanges, int jobIndex);

            public unsafe static void Execute(ref ContactsJobData<T> jobData, IntPtr additionalData,
                IntPtr bufferRangePatchData, ref JobRanges jobRanges, int jobIndex)
            {
                var reader = new Havok.Physics.HpBlockStreamReader(jobData.ManifoldStream);
                int* pluginIndexToLocal = jobData.PluginIndexToLocal->Data;
                while (reader.HasItems)
                {
                    var header = (Havok.Physics.HpManifoldStreamHeader*)reader.ReadPtr<Havok.Physics.HpManifoldStreamHeader>();
                    int numManifolds = header->NumManifolds;

                    int bodyIndexA = pluginIndexToLocal[header->BodyIds.BodyIndexA & 0x00ffffff];
                    int bodyIndexB = pluginIndexToLocal[header->BodyIds.BodyIndexB & 0x00ffffff];

                    var userHeader = new ModifiableContactHeader();
                    userHeader.ContactHeader.BodyPair = new BodyIndexPair
                    {
                        BodyIndexA = bodyIndexA,
                        BodyIndexB = bodyIndexB
                    };
                    userHeader.EntityPair = new EntityPair
                    {
                        EntityA = jobData.Bodies[bodyIndexA].Entity,
                        EntityB = jobData.Bodies[bodyIndexB].Entity
                    };

                    while (numManifolds-- > 0)
                    {
                        var manifold = (Havok.Physics.HpManifold*)reader.ReadPtr<Havok.Physics.HpManifold>();

                        userHeader.ContactHeader.NumContacts = manifold->NumPoints;
                        userHeader.ContactHeader.Normal = manifold->Normal.xyz;
                        var manifoldCache = manifold->m_CollisionCache;
                        userHeader.ContactHeader.CoefficientOfFriction = manifoldCache->m_friction.Value;
                        userHeader.ContactHeader.CoefficientOfRestitution = manifoldCache->m_restitution.Value;
                        userHeader.ContactHeader.ColliderKeys.ColliderKeyA.Value = manifold->m_ShapeKeyA;
                        userHeader.ContactHeader.ColliderKeys.ColliderKeyB.Value = manifold->m_ShapeKeyB;

                        Havok.Physics.HpPerManifoldProperty* cdp = manifoldCache->GetCustomPropertyStorage();
                        userHeader.ContactHeader.BodyCustomTags = cdp->m_bodyTagsPair;
                        userHeader.ContactHeader.JacobianFlags = (JacobianFlags)cdp->m_jacobianFlags;

                        for (int p = 0; p < manifold->NumPoints; p++)
                        {
                            var userContact = new ModifiableContactPoint
                            {
                                Index = p,
                                ContactPoint = new ContactPoint
                                {
                                    Position = new float3(manifold->Positions[p * 4 + 0], manifold->Positions[p * 4 + 1], manifold->Positions[p * 4 + 2]),
                                    Distance = manifold->Distances[p],
                                }
                            };

                            jobData.UserJobData.Execute(ref userHeader, ref userContact);

                            if (userContact.Modified)
                            {
                                manifold->Positions[p * 4 + 0] = userContact.ContactPoint.Position.x;
                                manifold->Positions[p * 4 + 1] = userContact.ContactPoint.Position.y;
                                manifold->Positions[p * 4 + 2] = userContact.ContactPoint.Position.z;
                                manifold->Distances[p] = userContact.ContactPoint.Distance;
                            }

                            if (userHeader.Modified)
                            {
                                manifold->Normal.xyz = userHeader.ContactHeader.Normal;
                                manifoldCache->m_friction.Value = userHeader.ContactHeader.CoefficientOfFriction;
                                manifoldCache->m_restitution.Value = userHeader.ContactHeader.CoefficientOfRestitution;
                                cdp->m_jacobianFlags = (byte)userHeader.ContactHeader.JacobianFlags;

                                if ((cdp->m_jacobianFlags & (byte)JacobianFlags.Disabled) != 0)
                                {
                                    manifold->m_ManifoldType = 3; // hknpManifoldType::DISABLED
                                    manifoldCache->m_collisionFlags = 1 << 9; // hknpCollisionFlags::DONT_BUILD_CONTACT_JACOBIANS
                                }
                                else
                                {
                                    // Not disabled, so check for other modifications.

                                    if ((cdp->m_jacobianFlags & (byte)JacobianFlags.EnableMassFactors) != 0)
                                    {
                                        manifold->m_DataFields |= 1 << 1; // hknpManifold::INERTIA_MODIFIED
                                        manifold->m_DataFields &= 0xfb; // ~CONTAINS_TRIANGLE

                                        var mp = (MassFactors*)UnsafeUtility.AddressOf(ref manifold->m_Scratch[0]);
                                        mp->InverseInertiaFactorA = new float3(1);
                                        mp->InverseMassFactorA = 1.0f;
                                        mp->InverseInertiaFactorB = new float3(1);
                                        mp->InverseMassFactorB = 1.0f;
                                    }

                                    if ((cdp->m_jacobianFlags & (byte)JacobianFlags.IsTrigger) != 0)
                                    {
                                        manifold->m_ManifoldType = 1; // hknpManifoldType::TRIGGER
                                    }

                                    if ((cdp->m_jacobianFlags & (byte)JacobianFlags.EnableSurfaceVelocity) != 0)
                                    {
                                        manifoldCache->m_collisionFlags |= 1 << 25; // hknpCollisionFlags::ENABLE_SURFACE_VELOCITY
                                    }

                                    if (userHeader.ContactHeader.CoefficientOfRestitution != 0)
                                    {
                                        manifoldCache->m_collisionFlags |= 1 << 20; // hknpCollisionFlags::ENABLE_RESTITUTION
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
