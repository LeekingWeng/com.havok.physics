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
        private readonly HpBlockStream* m_EventDataStream;

        private readonly NativeSlice<RigidBody> m_Bodies;

        internal HavokTriggerEvents(HpBlockStream* eventDataStream, NativeSlice<RigidBody> bodies)
        {
            m_EventDataStream = eventDataStream;
            m_Bodies = bodies;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(m_EventDataStream, m_Bodies);
        }

        public struct Enumerator /* : IEnumerator<TriggerEvent> */
        {
            private HpBlockStreamReader m_Reader;
            private readonly NativeSlice<RigidBody> m_Bodies;
            public TriggerEvent Current { get; private set; }

            internal Enumerator(HpBlockStream* stream, NativeSlice<RigidBody> bodies)
            {
                m_Reader = new HpBlockStreamReader(stream);
                Current = default;

                m_Bodies = bodies;
            }

            public bool MoveNext()
            {
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
