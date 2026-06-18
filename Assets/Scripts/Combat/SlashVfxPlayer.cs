using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace BeatHero.Combat
{
    // 참격 VFX 재생 전용 컴포넌트.
    // 할당된 AnimationClip을 컨트롤러 없이 PlayableGraph로 단발 재생하고,
    // 클립 길이가 지나면 전용 VFX 오브젝트를 자동 비활성화한다.
    // 평타(MonsterView)·처치 피니셔(PlayerController)가 이 하나를 공통으로 사용한다.
    public class SlashVfxPlayer : MonoBehaviour
    {
        [SerializeField] private Animator _animator;          // 전용 VFX 오브젝트의 Animator
        [SerializeField] private Vector3  _offset = Vector3.zero;

        private PlayableGraph _graph;
        private Coroutine     _stopRoutine;

        private void Awake()
        {
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_animator != null) _animator.gameObject.SetActive(false);
        }

        // clip을 worldPos(+offset) 위치에서 1회 재생. 연타 시 이전 재생을 안전하게 재시작.
        public void Play(AnimationClip clip, Vector3 worldPos)
        {
            if (_animator == null || clip == null) return;

            _animator.transform.position = worldPos + _offset;
            if (!_animator.gameObject.activeSelf) _animator.gameObject.SetActive(true);

            if (_graph.IsValid()) _graph.Destroy();
            AnimationPlayableUtilities.PlayClip(_animator, clip, out _graph);

            if (_stopRoutine != null) StopCoroutine(_stopRoutine);
            _stopRoutine = StartCoroutine(StopAfter(clip.length));
        }

        private IEnumerator StopAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_graph.IsValid()) _graph.Destroy();
            if (_animator != null) _animator.gameObject.SetActive(false);
            _stopRoutine = null;
        }

        private void OnDisable()
        {
            if (_stopRoutine != null) { StopCoroutine(_stopRoutine); _stopRoutine = null; }
            if (_graph.IsValid()) _graph.Destroy();
        }
    }
}
