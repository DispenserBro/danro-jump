using UnityEngine;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Общие правила поиска игрока и допуска player-contact событий.
    /// </summary>
    public static class PlayerContactUtility
    {
        public static bool TryGetAlivePlayer(Component source, out JumpPlayerController player)
        {
            player = source != null ? source.GetComponentInParent<JumpPlayerController>() : null;
            return player != null && !player.IsDead && !player.IgnoresEnvironmentInteractions;
        }
    }
}
