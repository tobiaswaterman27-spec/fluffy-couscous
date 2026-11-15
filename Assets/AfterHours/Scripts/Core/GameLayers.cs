using UnityEngine;

namespace AfterHours.Core
{
    /// <summary>
    /// Central list of physics layer names used by the game.
    /// The layers are created automatically by the editor menu "After Hours > 1 - Set Up Project".
    /// </summary>
    public static class GameLayers
    {
        public const string PlayerName = "Player";
        public const string LocalBodyName = "LocalPlayerBody";
        public const string CreatureName = "Creature";
        public const string InteractableName = "Interactable";
        public const string PropName = "Prop";
        public const string TriggerZoneName = "TriggerZone";

        public static readonly string[] AllNames =
        {
            PlayerName, LocalBodyName, CreatureName, InteractableName, PropName, TriggerZoneName
        };

        public static int PlayerLayer => LayerMask.NameToLayer(PlayerName);
        public static int LocalBodyLayer => LayerMask.NameToLayer(LocalBodyName);
        public static int CreatureLayer => LayerMask.NameToLayer(CreatureName);
        public static int InteractableLayer => LayerMask.NameToLayer(InteractableName);
        public static int PropLayer => LayerMask.NameToLayer(PropName);
        public static int TriggerZoneLayer => LayerMask.NameToLayer(TriggerZoneName);

        /// <summary>
        /// Everything that physically blocks a character body: architecture, furniture, props,
        /// other characters. Excludes purely visual body meshes, trigger volumes and "Ignore Raycast".
        /// </summary>
        public static int CharacterBlockingMask
        {
            get
            {
                int mask = Physics.DefaultRaycastLayers;
                mask = Exclude(mask, LocalBodyLayer);
                mask = Exclude(mask, TriggerZoneLayer);
                return mask;
            }
        }

        public static bool AllLayersExist()
        {
            foreach (string name in AllNames)
            {
                if (LayerMask.NameToLayer(name) < 0) return false;
            }
            return true;
        }

        static int Exclude(int mask, int layer)
        {
            return layer < 0 ? mask : mask & ~(1 << layer);
        }
    }
}
