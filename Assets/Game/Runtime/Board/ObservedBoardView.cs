using System;
using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    /// <summary>
    /// 单个个人棋盘画面容器。切换观察玩家只更换动态内容，静态棋盘保持不变。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObservedBoardView : MonoBehaviour
    {
        [SerializeField] private Transform staticBoardRoot;
        [SerializeField] private Transform routeRoot;
        [SerializeField] private Transform pieceRoot;
        [SerializeField] private Transform enemyRoot;
        [SerializeField] private Transform effectRoot;

        public event Action<int> ObservedPlayerChanged;

        public int ObservedPlayerId { get; private set; }
        public Transform StaticBoardRoot => staticBoardRoot;
        public Transform RouteRoot => routeRoot;
        public Transform PieceRoot => pieceRoot;
        public Transform EnemyRoot => enemyRoot;
        public Transform EffectRoot => effectRoot;

        public void SetObservedPlayer(int playerId)
        {
            if (playerId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId));
            }

            if (ObservedPlayerId == playerId)
            {
                return;
            }

            ObservedPlayerId = playerId;
            ObservedPlayerChanged?.Invoke(playerId);
        }
    }
}
