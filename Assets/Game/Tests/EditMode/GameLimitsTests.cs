using NUnit.Framework;

namespace ProtectTree.Core.Tests
{
    public sealed class GameLimitsTests
    {
        [Test]
        public void MaxPlayers_IsFour()
        {
            Assert.That(GameLimits.MaxPlayers, Is.EqualTo(4));
        }
    }
}

