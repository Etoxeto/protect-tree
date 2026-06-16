using ProtectTree.Core;

namespace ProtectTree.LuaContracts
{
    public static class LuaLearningApi
    {
        public static int MaxPlayers => GameLimits.MaxPlayers;

        public static int Add(int left, int right)
        {
            return left + right;
        }
    }
}
