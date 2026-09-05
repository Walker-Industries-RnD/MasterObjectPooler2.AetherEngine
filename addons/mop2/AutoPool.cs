using Godot;

namespace QFSW.MOP2
{
    /// <summary>
    /// Automatically releases an object after the specified amount of time has surpassed.
    /// </summary>
    [GlobalClass]
    public partial class AutoPool : PoolableNode
    {
        [Export]
        private float _poolTimer = 1;

        [Export]
        private bool _scaledTime = true;

        private float _elapsedTime;

        public override void _Ready()
        {
            // A pooled object stays in the tree between uses, so re-arm the timer whenever it is re-activated
            // (made visible) by the pool. This mirrors Unity's AutoPool resetting in OnEnable.
            VisibilityChanged += OnVisibilityChanged;
            _elapsedTime = 0;
        }

        private void OnVisibilityChanged()
        {
            if (Visible)
            {
                _elapsedTime = 0;
            }
        }

        public override void _Process(double delta)
        {
            float scaledDelta = (float)delta;
            if (_scaledTime)
            {
                _elapsedTime += scaledDelta;
            }
            else
            {
                float timeScale = (float)Engine.TimeScale;
                _elapsedTime += timeScale > 0 ? scaledDelta / timeScale : scaledDelta;
            }

            if (_elapsedTime > _poolTimer && PoolReady) { Release(); }
        }
    }
}
