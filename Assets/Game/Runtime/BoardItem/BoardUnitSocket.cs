using UnityEngine;

namespace ProtectTree.Runtime
{
    public class BoardUnitSocket : MonoBehaviour
    {
        [SerializeField] private Transform FirePoint;
        [SerializeField] private Transform HitPoint;
        public Animator animator;
        public SpriteRenderer[] Eyes;

        public Vector3 GetFirePointWorldPosition(Vector3 fallback)
        {
            return ResolveFirePoint() != null
                ? ResolveFirePoint().position
                : fallback;
        }

        public Vector3 GetHitPointWorldPosition(Vector3 fallback)
        {
            return ResolveHitPoint() != null
                ? ResolveHitPoint().position
                : fallback;
        }

        private Transform ResolveFirePoint()
        {
            if (FirePoint != null)
            {
                return FirePoint;
            }

            FirePoint = FindChild("FirePoint");
            return FirePoint;
        }

        private Transform ResolveHitPoint()
        {
            if (HitPoint != null)
            {
                return HitPoint;
            }

            HitPoint = FindChild("HitPoint");
            return HitPoint;
        }

        private Transform FindChild(string childName)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
