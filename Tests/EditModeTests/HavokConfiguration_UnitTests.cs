using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Physics.Editor;
using UnityEditor;

namespace Havok.Physics.Tests.Authoring
{
    class HavokConfiguration_UnitTests
    {
        // TODO : Add actual tests!

        static IEnumerable k_GetMatrixStatusMessageTestCases = new[]
        {
            new TestCaseData(new[] { 2,  2 }, 4).Returns(MessageType.None).SetName("2 + 2 = 4"),
        };

        [TestCaseSource(nameof(k_GetMatrixStatusMessageTestCases))]
        public MessageType HavokConfiguration_UnitTests_Sum(
            IReadOnlyList<int> ints, int sum
        )
        {
            var isum = 0;
            foreach (var i in ints) { isum += i; }

            Assert.That(isum, Is.EqualTo(sum));

            return MessageType.None;
        }
    }
}
