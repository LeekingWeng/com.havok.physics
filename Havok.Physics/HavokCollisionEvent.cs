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
        private readonly HpLinkedRange* m_EventDataRange;

        private readonly NativeArray<Velocity> m_InputVelocities;
        private readonly float m_TimeStep;

        internal HavokCollisionEvents(HpLinkedRange* eventDataRange, NativeArray<Velocity> inputVelocities, float timeStep)
        {
            m_EventDataRange = eventDataRange;

            m_InputVelocities = inputVelocities;
            m_TimeStep = timeStep;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(m_EventDataRange, m_InputVelocities, m_TimeStep);
        }

        public struct Enumerator /* : IEnumerator<CollisionEvent> */
        {
            private HpLinkedRange* m_Range;
            private HpBlockStreamReader m_Reader;
            private CollisionEventDataRef m_Current;

            private readonly NativeArray<Velocity> m_InputVelocities;
            private readonly float m_TimeStep;

            public CollisionEvent Current
            {
                get => m_Current.Value.CreateCollisionEvent(m_TimeStep, m_InputVelocities);
            }

            internal Enumerator(HpLinkedRange* range, NativeArray<Velocity> inputVelocities, float timeStep)
            {
                m_Range = range;
                m_Reader = new HpBlockStreamReader(m_Range);

                m_InputVelocities = inputVelocities;
                m_TimeStep = timeStep;

                unsafe
                {
                    m_Current = default;
                }
            }

            public bool MoveNext()
            {
                if (!m_Reader.HasItems && m_Range->m_next != null)
                {
                    m_Range = m_Range->m_next;
                    m_Reader = new HpBlockStreamReader(m_Range);
                }

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
