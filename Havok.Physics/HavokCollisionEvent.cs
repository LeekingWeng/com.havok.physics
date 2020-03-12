using Havok.Physics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace Unity.Physics
{
    // A stream of collision events.
    // This is a value type, which means it can be used in Burst jobs (unlike IEnumerable<CollisionEvent>).
    public unsafe struct HavokCollisionEvents /* : IEnumerable<CollisionEvent> */
    {
        [NativeDisableUnsafePtrRestriction]
        private readonly HpBlockStream* m_EventDataStream;

        private readonly NativeSlice<RigidBody> m_Bodies;
        private readonly NativeSlice<Velocity> m_InputVelocities;
        private readonly float m_TimeStep;

        internal HavokCollisionEvents(HpBlockStream* eventDataStream, NativeSlice<RigidBody> bodies, NativeSlice<Velocity> inputVelocities, float timeStep)
        {
            m_EventDataStream = eventDataStream;
            m_Bodies = bodies;
            m_InputVelocities = inputVelocities;
            m_TimeStep = timeStep;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(m_EventDataStream, m_Bodies, m_InputVelocities, m_TimeStep);
        }

        public struct Enumerator /* : IEnumerator<CollisionEvent> */
        {
            private HpBlockStreamReader m_Reader;
            private CollisionEventDataRef m_Current;

            private readonly NativeSlice<RigidBody> m_Bodies;
            private readonly NativeSlice<Velocity> m_InputVelocities;
            private readonly float m_TimeStep;

            public CollisionEvent Current
            {
                get => m_Current.Value.CreateCollisionEvent(m_TimeStep, m_Bodies, m_InputVelocities);
            }

            internal Enumerator(HpBlockStream* stream, NativeSlice<RigidBody> bodies, NativeSlice<Velocity> inputVelocities, float timeStep)
            {
                m_Reader = new HpBlockStreamReader(stream);

                m_Bodies = bodies;
                m_InputVelocities = inputVelocities;
                m_TimeStep = timeStep;

                unsafe
                {
                    m_Current = default;
                }
            }

            public bool MoveNext()
            {
                if (m_Reader.HasItems)
                {
                    // Read the size first
                    int size = m_Reader.Read<int>();
                    m_Current = new CollisionEventDataRef((CollisionEventData*)(m_Reader.Peek()));
                    m_Reader.Advance(size);
                    return true;
                }
                return false;
            }
        }
    }
}
