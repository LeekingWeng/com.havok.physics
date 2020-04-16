using Havok.Physics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Physics
{
    // A stream of trigger events.
    // This is a value type, which means it can be used in Burst jobs (unlike IEnumerable<TriggerEvent>).
    public unsafe struct HavokTriggerEvents /* : IEnumerable<TriggerEvent> */
    {
        [NativeDisableUnsafePtrRestriction]
        private readonly HpLinkedRange* m_EventDataRange;

        private readonly NativeSlice<RigidBody> m_Bodies;

        internal HavokTriggerEvents(HpLinkedRange* eventDataRange, NativeSlice<RigidBody> bodies)
        {
            m_EventDataRange = eventDataRange;
            m_Bodies = bodies;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(m_EventDataRange, m_Bodies);
        }

        public struct Enumerator /* : IEnumerator<TriggerEvent> */
        {
            private HpLinkedRange* m_Range;
            private HpBlockStreamReader m_Reader;
            private readonly NativeSlice<RigidBody> m_Bodies;
            public TriggerEvent Current { get; private set; }

            internal Enumerator(HpLinkedRange* range, NativeSlice<RigidBody> bodies)
            {
                m_Range = range;
                m_Reader = new HpBlockStreamReader(m_Range);
                Current = default;

                m_Bodies = bodies;
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
                    var eventData = (TriggerEventData*)m_Reader.Peek();

                    Current = eventData->CreateTriggerEvent(m_Bodies);

                    m_Reader.Advance(sizeof(TriggerEventData));
                    return true;
                }
                return false;
            }
        }
    }
}
