using ProtectTree.Core.Match;

namespace ProtectTree.Runtime.Presentation
{
    public interface IPieceSellDropZone
    {
        bool CanAcceptPieceSellDrop(MatchSceneContext context, PieceSnapshot piece);
    }
}
