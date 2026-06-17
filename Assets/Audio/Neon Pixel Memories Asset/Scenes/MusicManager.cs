namespace NeonPixelMemories
{
    using UnityEngine;
    using UnityEngine.UI;
    using TMPro;

    public class MusicManager : MonoBehaviour
    {
        public AudioSource audioSource;
        public AudioClip[] musicClips;
        public Transform listParent;
        public GameObject buttonPrefab;

        private AudioClip currentClip;

        void Start()
        {
            foreach (AudioClip clip in musicClips)
            {
                GameObject newButton = Instantiate(buttonPrefab, listParent);

                TMP_Text btnText = newButton.GetComponentInChildren<TMP_Text>();
                btnText.text = clip.name;

                Button btn = newButton.GetComponent<Button>();
                btn.onClick.AddListener(() => PlayClip(clip));
            }
        }

        void PlayClip(AudioClip clip)
        {
            if (currentClip == clip && audioSource.isPlaying)
            {
                audioSource.Stop();
                return;
            }

            currentClip = clip;
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}