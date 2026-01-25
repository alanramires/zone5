using UnityEngine;
using UnityEngine.UI;

namespace Zone5
{
    public class PanelMancheDefaults : MonoBehaviour
    {
        [Header("Manche Parts")]
        [SerializeField] private Toggle toggleNormal;
        [SerializeField] private Toggle toggleAcrobacia;
        [SerializeField] private Toggle togglePosComb;
        [SerializeField] private GameObject rowNormal;
        [SerializeField] private GameObject rowAcrobacia;
        [SerializeField] private GameObject rowPosComb;
        [SerializeField] private PanelMancheController panelMancheController;

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
            if (toggleNormal != null) toggleNormal.SetIsOnWithoutNotify(true);
            if (toggleAcrobacia != null) toggleAcrobacia.SetIsOnWithoutNotify(false);
            if (togglePosComb != null) togglePosComb.SetIsOnWithoutNotify(false);

            if (rowNormal != null) rowNormal.SetActive(true);
            if (rowAcrobacia != null) rowAcrobacia.SetActive(false);
            if (rowPosComb != null) rowPosComb.SetActive(false);

            if (panelMancheController != null)
                panelMancheController.ApplyModeFromToggles();
        }
    }
}
