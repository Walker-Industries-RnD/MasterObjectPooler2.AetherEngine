namespace QFSW.MOP2
{
    /// <summary>
    /// Allows the object to receive information about the pool that it is a part of.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Initializes the object instance with the parent pool.
        /// </summary>
        /// <param name="pool">The pool that this object belongs to.</param>
        void InitializeTemplate(ObjectPool pool);
    }
}
