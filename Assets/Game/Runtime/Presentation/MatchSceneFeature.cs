using ProtectTree.Runtime.Lua;
using UnityEngine;

namespace ProtectTree.Runtime.Presentation
{
    public abstract class MatchSceneFeature : MonoBehaviour
    {
        public virtual int RefreshOrder => 0;

        public virtual void OnRuntimeChanged(LuaRuntime runtime)
        {
        }

        public virtual void Refresh(MatchSceneContext context)
        {
        }

        public virtual void OnRuntimeUnavailable()
        {
        }
    }
}
