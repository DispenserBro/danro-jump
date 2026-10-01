using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace DanroJump.Gameplay
{
    /// <summary>
    /// Управляет визуальными объектами экипировки, которые крепятся к игроку.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerEquipmentAttachment : MonoBehaviour
    {
        [SerializeField] private Transform attachmentRoot;
        [SerializeField] private string attachmentRootPath = "Body/Dress/Equipment placeholder";

        private readonly Dictionary<string, GameObject> attachedObjectsBySlot = new();
        private readonly HashSet<string> reservedSlotIds = new();

        /// <summary>
        /// Текущая точка крепления экипировки.
        /// </summary>
        public Transform AttachmentRoot
        {
            get
            {
                ResolveAttachmentRoot();
                return attachmentRoot;
            }
        }

        /// <summary>
        /// Прикрепляет существующий объект экипировки к указанному слоту игрока.
        /// </summary>
        public GameObject Attach(GameObject equipmentObject, string slotId, bool replaceExisting = true)
        {
            if (equipmentObject == null)
            {
                return null;
            }

            var root = AttachmentRoot;
            if (root == null)
            {
                Debug.LogWarning($"{nameof(PlayerEquipmentAttachment)} could not find attachment root '{attachmentRootPath}'.", this);
                return null;
            }

            var normalizedSlotId = NormalizeSlotId(slotId, equipmentObject.name);
            reservedSlotIds.Remove(normalizedSlotId);
            if (!replaceExisting && attachedObjectsBySlot.TryGetValue(normalizedSlotId, out var currentObject) && currentObject != null)
            {
                return currentObject;
            }

            if (replaceExisting)
            {
                ClearSlot(normalizedSlotId);
            }

            var localScale = equipmentObject.transform.localScale;
            equipmentObject.transform.SetParent(root, false);
            equipmentObject.transform.localPosition = Vector3.zero;
            equipmentObject.transform.localRotation = Quaternion.identity;
            equipmentObject.transform.localScale = localScale;
            attachedObjectsBySlot[normalizedSlotId] = equipmentObject;
            return equipmentObject;
        }

        /// <summary>
        /// Резервирует слот до отложенного attach, чтобы второй pickup не заменил первый в тот же кадр.
        /// </summary>
        public bool TryReserveSlot(string slotId)
        {
            var normalizedSlotId = NormalizeSlotId(slotId, slotId);
            if (string.IsNullOrWhiteSpace(normalizedSlotId))
            {
                return false;
            }

            if (attachedObjectsBySlot.TryGetValue(normalizedSlotId, out var attachedObject) && attachedObject != null)
            {
                return false;
            }

            if (reservedSlotIds.Contains(normalizedSlotId))
            {
                return false;
            }

            reservedSlotIds.Add(normalizedSlotId);
            return true;
        }

        /// <summary>
        /// Снимает резервирование слота, если отложенное прикрепление не состоялось.
        /// </summary>
        public void ReleaseReservation(string slotId)
        {
            var normalizedSlotId = NormalizeSlotId(slotId, slotId);
            if (!string.IsNullOrWhiteSpace(normalizedSlotId))
            {
                reservedSlotIds.Remove(normalizedSlotId);
            }
        }

        /// <summary>
        /// Возвращает уже прикрепленную экипировку в указанном слоте.
        /// </summary>
        public bool TryGetAttached(string slotId, out GameObject attachedObject)
        {
            attachedObject = null;
            var normalizedSlotId = NormalizeSlotId(slotId, slotId);
            if (string.IsNullOrWhiteSpace(normalizedSlotId))
            {
                return false;
            }

            if (!attachedObjectsBySlot.TryGetValue(normalizedSlotId, out attachedObject) || attachedObject == null)
            {
                attachedObjectsBySlot.Remove(normalizedSlotId);
                attachedObject = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Отвязывает visual от слота, не уничтожая его.
        /// </summary>
        public bool TryDetach(string slotId, GameObject expectedObject, out GameObject detachedObject)
        {
            detachedObject = null;
            var normalizedSlotId = NormalizeSlotId(slotId, slotId);
            if (string.IsNullOrWhiteSpace(normalizedSlotId))
            {
                return false;
            }

            reservedSlotIds.Remove(normalizedSlotId);

            if (!attachedObjectsBySlot.TryGetValue(normalizedSlotId, out var attachedObject) || attachedObject == null)
            {
                attachedObjectsBySlot.Remove(normalizedSlotId);
                return false;
            }

            if (expectedObject != null && attachedObject != expectedObject)
            {
                return false;
            }

            attachedObjectsBySlot.Remove(normalizedSlotId);
            attachedObject.transform.SetParent(null, true);
            detachedObject = attachedObject;
            return true;
        }

        /// <summary>
        /// Удаляет visual из указанного слота экипировки.
        /// </summary>
        public void ClearSlot(string slotId)
        {
            var normalizedSlotId = NormalizeSlotId(slotId, slotId);
            if (string.IsNullOrWhiteSpace(normalizedSlotId))
            {
                return;
            }

            reservedSlotIds.Remove(normalizedSlotId);

            if (!attachedObjectsBySlot.TryGetValue(normalizedSlotId, out var attachedObject))
            {
                return;
            }

            if (attachedObject != null)
            {
                StopVisualEffects(attachedObject);
                attachedObject.transform.SetParent(null, true);
                Destroy(attachedObject);
            }

            attachedObjectsBySlot.Remove(normalizedSlotId);
        }

        private static void StopVisualEffects(GameObject attachedObject)
        {
            if (attachedObject == null)
            {
                return;
            }

            var effects = attachedObject.GetComponentsInChildren<VisualEffect>(true);
            for (var index = 0; index < effects.Length; index++)
            {
                var effect = effects[index];
                if (effect == null)
                {
                    continue;
                }

                effect.Stop();
                effect.enabled = false;
            }
        }

        private void Awake()
        {
            ResolveAttachmentRoot();
        }

        private void OnValidate()
        {
            ResolveAttachmentRoot();
        }

        private void ResolveAttachmentRoot()
        {
            if (attachmentRoot != null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(attachmentRootPath))
            {
                attachmentRoot = transform.Find(attachmentRootPath);
            }
        }

        private static string NormalizeSlotId(string slotId, string fallback)
        {
            return string.IsNullOrWhiteSpace(slotId) ? fallback : slotId;
        }
    }
}
