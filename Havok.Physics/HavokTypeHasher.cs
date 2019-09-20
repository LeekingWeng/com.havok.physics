namespace Havok.Physics
{
    // Builds a deterministic hash for a type.
    // It is intended only to detect changes in the type.
    internal struct TypeHasher
    {
        // The cumulative hash of all the types this checker has seen
        public ulong Value { get; private set; }

        // Generate a deterministic hash for a given type.
        // This is calculated based on the name, field name, types and sizes.
        public void AddType(System.Type t)
        {
            AddStringHash(t.Name);
            AddIntHash(System.Runtime.InteropServices.Marshal.SizeOf(t));
            System.Reflection.FieldInfo[] fields = t.GetFields();
            AddIntHash(fields.Length);
            foreach (System.Reflection.FieldInfo f in fields)
            {
                AddStringHash(f.Name);
                AddStringHash(f.FieldType.Name);
            }
        }

        // Hash a string as bytes, by xoring and shifting
        // The hash is intended only to detect changes
        public void AddStringHash(string s)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
            foreach (byte b in bytes)
            {
                Value = (((Value >> 56) ^ b) | (Value << 8));
            }
        }

        // Hash a 32-bit value, by xoring and shifting
        // The hash is intended only to detect changes
        public void AddIntHash(int i)
        {
            ulong i64 = (ulong)i;
            Value = (((Value >> 32) ^ i64) | (Value << 32));
        }

        public static ulong CalculateTypeCheckValue(System.Type t)
        {
            var check = new TypeHasher();
            check.AddType(t);
            return check.Value;
        }
    }
}