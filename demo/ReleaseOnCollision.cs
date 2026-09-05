using Godot;

namespace QFSW.MOP2.Demo
{
    /// <summary>
    /// Releases the object back to its pool when it collides with a body on the configured collision mask,
    /// optionally spawning a death FX from another pool at the point of collision.
    /// </summary>
    /// <remarks>
    /// Godot port of the MOP2 demo script. Requires <c>ContactMonitor</c> and a non-zero <c>MaxContactsReported</c>
    /// so that the <see cref="RigidBody3D.BodyEntered"/> signal fires; both are enabled in <see cref="_Ready"/>.
    /// </remarks>
    [GlobalClass]
    public partial class ReleaseOnCollision : RigidBody3D, IPoolable
    {
        [Export(PropertyHint.Layers3DPhysics, "Only collisions with bodies on these layers will trigger a release.")]
        private uint _collisionLayer = 0;

        /// <summary>Optional pool to spawn a death FX from at the collision point.</summary>
        [Export]
        private ObjectPool _deathFX = null;

        private ObjectPool _parentPool;

        void IPoolable.InitializeTemplate(ObjectPool pool)
        {
            _parentPool = pool;
        }

        public override void _Ready()
        {
            ContactMonitor = true;
            if (MaxContactsReported < 1) { MaxContactsReported = 1; }
            BodyEntered += OnBodyEntered;
        }

        private void OnBodyEntered(Node body)
        {
            uint bodyLayer = body is CollisionObject3D collisionObject ? collisionObject.CollisionLayer : 0;
            if ((_collisionLayer & bodyLayer) != 0)
            {
                _parentPool?.Release(this);
                if (_deathFX != null)
                {
                    _deathFX.GetObject(GlobalPosition);
                }
            }
        }
    }
}
