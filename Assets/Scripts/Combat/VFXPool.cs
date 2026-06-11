using UnityEngine;
using UnityEngine.Pool;

namespace BeatHero.Combat
{
    public class VFXPool : MonoBehaviour
    {
        public static VFXPool Instance { get; private set; }

        [SerializeField] private VFXPlayer _playerPrefab;
        [SerializeField] private int       _defaultCapacity = 16;
        [SerializeField] private int       _maxSize         = 64;

        private ObjectPool<VFXPlayer> _pool;

        private void Awake()
        {
            Instance = this;
            _pool = new ObjectPool<VFXPlayer>(
                createFunc:      () => Instantiate(_playerPrefab, transform),
                actionOnGet:     p  => p.gameObject.SetActive(true),
                actionOnRelease: p  => p.gameObject.SetActive(false),
                actionOnDestroy: p  => Destroy(p.gameObject),
                collectionCheck: false,
                defaultCapacity: _defaultCapacity,
                maxSize:         _maxSize
            );
        }

        public void Play(Sprite[] frames, float fps, Vector3 worldPos)
        {
            if (frames == null || frames.Length == 0) return;
            var player = _pool.Get();
            player.Play(frames, fps, worldPos, () => _pool.Release(player));
        }
    }
}
