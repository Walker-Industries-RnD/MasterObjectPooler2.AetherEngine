using Godot;

namespace QFSW.MOP2
{
    /// <summary>
    /// Node that has a reference to its parent pool, and can thus be released to the pool without the user needing a reference to its pool, making it self contained.
    /// Usable either as a standalone script or as a base class for other scripts.
    /// </summary>
    /// <remarks>
    /// Godot port of MOP2's <c>PoolableMonoBehaviour</c>. Attach the script (or a subclass of it) to the root node of the
    /// pooled scene. Because it derives from <see cref="Node3D"/> it may be attached to any 3D node, including physics bodies.
    /// For 2D or Control based pooled scenes, implement <see cref="IPoolable"/> directly instead.
    /// </remarks>
    public partial class PoolableNode : Node3D, IPoolable
    {
        /// <summary>
        /// If its parent pool has been initialized/assigned yet.
        /// </summary>
        public bool PoolReady => _parentPool != null;

        private ObjectPool _parentPool;

        void IPoolable.InitializeTemplate(ObjectPool pool)
        {
            _parentPool = pool;
        }

        /// <summary>
        /// Releases the object and returns it back to its pool, effectively 'destroying' it from the scene.
        /// </summary>
        public void Release()
        {
            _parentPool.Release(this);
        }
    }
}
