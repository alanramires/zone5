using UnityEngine;
using UnityEngine.UI;

namespace Zone5
{
    public class PanelWeaponsDefaults : MonoBehaviour
    {
        [Header("Weapons Buttons")]
        [SerializeField] private Button buttonMissile;
        [SerializeField] private Button buttonGun;
        [SerializeField] private Button buttonOff;
        [SerializeField] private Button buttonConfirm;

        private UnitSpawner _spawner;

        private void OnEnable()
        {
            BindSpawner();
        }

        private void Start()
        {
            ApplyDefaults();
        }

        private void OnDisable()
        {
            UnbindSpawner();
        }

        private void BindSpawner()
        {
            if (_spawner != null) return;
            _spawner = FindFirstObjectByType<UnitSpawner>();
            if (_spawner != null)
                _spawner.OnSpawnCompleted += ApplyDefaults;
        }

        private void UnbindSpawner()
        {
            if (_spawner == null) return;
            _spawner.OnSpawnCompleted -= ApplyDefaults;
            _spawner = null;
        }

        public void ApplyDefaults()
        {
            if (buttonMissile != null) buttonMissile.interactable = false;
            if (buttonGun != null) buttonGun.interactable = false;
            if (buttonOff != null) buttonOff.interactable = false;
            if (buttonConfirm != null) buttonConfirm.interactable = false;
        }
    }
}
