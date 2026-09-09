using UnityEngine;

namespace FlappyVoice.Gameplay
{
    // TODO: development aid, remove before shipping. Lets the bird be flown from an on-screen slider
    // so pipe layout, collision and the note bar can be exercised without a microphone.
    public sealed class DevHeightSource : MonoBehaviour, IHeightSource
    {
        [SerializeField] private bool _enabled;
        [SerializeField] private float _height01 = 0.5f;

        public bool Enabled => _enabled;
        public float TargetHeight01 => _height01;
        public bool IsActive => _enabled;

        public void SetEnabled(bool value)
        {
            _enabled = value;
        }

        public void SetHeight01(float value)
        {
            _height01 = Mathf.Clamp01(value);
        }
    }
}
