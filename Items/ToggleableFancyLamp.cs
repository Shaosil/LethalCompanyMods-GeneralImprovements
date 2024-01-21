using UnityEngine;

namespace GeneralImprovements.Items
{
    public class ToggleableFancyLamp : PhysicsProp
    {
        private Light _light;
        private AudioSource _audioSource;

        public AudioClip[] AudioClips;

        public override void Start()
        {
            _light = GetComponentInChildren<Light>();
            _audioSource = GetComponentInChildren<AudioSource>();
            base.Start();
            isBeingUsed = true;
        }

        public override void ItemActivate(bool used, bool buttonDown = true)
        {
            _light.enabled = used;

            if (_audioSource != null && AudioClips != null && AudioClips.Length > 0)
            {
                _audioSource.PlayOneShot(AudioClips[Random.Range(0, AudioClips.Length)]);
                RoundManager.Instance.PlayAudibleNoise(transform.position, 7f, 0.4f, 0, isInElevator && StartOfRound.Instance.hangarDoorsClosed, 0);
            }
        }
    }
}