using System;
using ProtectTree.Core.Match;
using ProtectTree.Runtime.Presentation;
using UnityEngine;

namespace ProtectTree.Runtime.UI
{
    [DisallowMultipleComponent]
    public sealed class UIPlayerInfos : MatchSceneFeature
    {
        [SerializeField] private UIPlayerInfo[] playerInfos = new UIPlayerInfo[4];
        [SerializeField] private string defaultAvatarResourcePath =
            "UI/Infos/Player/blade_of_god_rick";

        private Sprite _defaultAvatar;

        private void Awake()
        {
            _defaultAvatar = UIResourceLoader.LoadSprite(defaultAvatarResourcePath);
            HideAll();
        }

        public override void Refresh(MatchSceneContext context)
        {
            if (context.Players == null || playerInfos == null)
            {
                HideAll();
                return;
            }

            int index = 0;
            foreach (PlayerSnapshot player in context.Players.Players)
            {
                if (index >= playerInfos.Length)
                {
                    break;
                }

                Action<int> observePlayer = context.RequestObservePlayer;
                playerInfos[index]?.Render(
                    player,
                    _defaultAvatar,
                    player.PlayerId == context.ObservedPlayerId,
                    observePlayer);
                index++;
            }

            for (; index < playerInfos.Length; index++)
            {
                playerInfos[index]?.Hide();
            }
        }

        public override void OnRuntimeUnavailable()
        {
            HideAll();
        }

        private void HideAll()
        {
            if (playerInfos == null)
            {
                return;
            }

            foreach (UIPlayerInfo playerInfo in playerInfos)
            {
                playerInfo?.Hide();
            }
        }
    }
}
