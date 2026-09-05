using System;
using System.Collections.Generic;
using Godot;

namespace QFSW.MOP2
{
    /// <summary>
    /// MasterObjectPooler manages various ObjectPools. By using a MasterObjectPooler, you can perform a large variety of pool operations with a named string reference
    /// instead of requiring an object reference to the ObjectPool. Furthermore, initialization of the pools is handled by the MOP.
    /// Pools can be added either via the inspector or at runtime.
    /// </summary>
    [GlobalClass]
    public partial class MasterObjectPooler : Node
    {
        [Export]
        private bool _singletonMode = false;

        [Export]
        private Godot.Collections.Array<ObjectPool> _pools = new Godot.Collections.Array<ObjectPool>();

        /// <summary>
        /// Singleton reference to the MOP. Only valid and set if the singleton option is enabled for the MOP.
        /// </summary>
        public static MasterObjectPooler Instance { get; private set; }

        private readonly Dictionary<string, ObjectPool> _poolTable = new Dictionary<string, ObjectPool>();

        #region Initialization
        public override void _EnterTree()
        {
            if (_singletonMode)
            {
                if (Instance == null)
                {
                    Instance = this;
                    // Reparent under the scene tree root so the MOP survives scene changes (Godot's equivalent of DontDestroyOnLoad).
                    if (GetParent() == GetTree().Root)
                    {
                        // Already a child of the root; nothing to do.
                    }
                }
                else
                {
                    QueueFree();
                    return;
                }
            }
        }

        public override void _Ready()
        {
            if (_singletonMode && Instance == this && GetParent() != GetTree().Root)
            {
                GD.PushWarning($"Singleton mode enabled for the Master Object Pooler '{Name}'. Reparenting to the scene tree root so it can be made scene persistent.");
                Reparent(GetTree().Root, keepGlobalTransform: false);
            }

            foreach (ObjectPool pool in _pools)
            {
                if (pool != null) { AddPool(pool); }
            }
        }

        public override void _ExitTree()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
        #endregion

        #region Internal
        private void DestroyPoolInternal(ObjectPool pool)
        {
            Node parent = pool.ObjectParent;
            pool.Purge();
            if (GodotObject.IsInstanceValid(parent)) { parent.QueueFree(); }
        }
        #endregion

        #region PoolManagement
        /// <summary>
        /// Adds an ObjectPool to the MasterObjectPooler and initializes it.
        /// </summary>
        /// <param name="pool">The ObjectPool to add to the MasterObjectPooler.</param>
        public void AddPool(ObjectPool pool) { AddPool(pool.PoolName, pool); }

        /// <summary>
        /// Adds an ObjectPool to the MasterObjectPooler and initializes it.
        /// </summary>
        /// <param name="poolName">Override for the named string reference to use for this pool. By default uses the ObjectPool's name.</param>
        /// <param name="pool">The ObjectPool to add to the MasterObjectPooler.</param>
        public void AddPool(string poolName, ObjectPool pool)
        {
            pool.Initialize();

            Node holder = pool.ObjectParent;
            if (holder.GetParent() != this)
            {
                holder.Reparent(this, keepGlobalTransform: false);
            }

            if (_poolTable.ContainsKey(poolName))
            {
                GD.PushWarning($"{poolName} could not be added to the pool table as a pool with the same name already exists");
            }
            else
            {
                _poolTable.Add(poolName, pool);
            }
        }

        /// <summary>
        /// Retrieves a pool.
        /// </summary>
        /// <param name="poolName">The name of the pool to retrieve.</param>
        /// <returns>The retrieved pool.</returns>
        public ObjectPool GetPool(string poolName)
        {
            if (_poolTable.TryGetValue(poolName, out ObjectPool pool))
            {
                return pool;
            }

            throw new ArgumentException($"Cannot get pool {poolName} as it is not present in the pool table");
        }

        /// <summary>
        /// Retrieves/adds a pool.
        /// </summary>
        /// <param name="poolName">The name of the pool to retrieve/add.</param>
        /// <returns>The retrieved pool.</returns>
        public ObjectPool this[string poolName]
        {
            get => GetPool(poolName);
            set
            {
                _poolTable.Remove(poolName);
                AddPool(poolName, value);
            }
        }

        /// <summary>
        /// Destroys every pool, purging all of their contents then removing them from the MasterObjectPooler.
        /// </summary>
        public void DestroyAllPools()
        {
            foreach (ObjectPool pool in _poolTable.Values)
            {
                DestroyPoolInternal(pool);
            }

            _poolTable.Clear();
        }

        /// <summary>
        /// Destroys a specified pool, purging its contents and removing it from the MasterObjectPooler.
        /// </summary>
        /// <param name="poolName">The pool to destroy.</param>
        public void DestroyPool(string poolName)
        {
            ObjectPool pool = GetPool(poolName);
            DestroyPoolInternal(pool);
            _poolTable.Remove(poolName);
        }
        #endregion

        #region GetObject/Component
        /// <summary>
        /// Gets an object from the specified pool.
        /// </summary>
        /// <param name="poolName">The name of the pool to get an object from.</param>
        /// <returns>The retrieved object.</returns>
        public Node GetObject(string poolName)
        {
            return GetPool(poolName).GetObject();
        }

        /// <summary>
        /// Gets an object from the specified pool.
        /// </summary>
        /// <param name="poolName">The name of the pool to get an object from.</param>
        /// <param name="position">The position to set the object to.</param>
        /// <returns>The retrieved object.</returns>
        public Node GetObject(string poolName, Vector3 position)
        {
            return GetPool(poolName).GetObject(position);
        }

        /// <summary>
        /// Gets an object from the specified pool.
        /// </summary>
        /// <param name="poolName">The name of the pool to get an object from.</param>
        /// <param name="position">The position to set the object to.</param>
        /// <param name="rotation">The rotation to set the object to.</param>
        /// <returns>The retrieved object.</returns>
        public Node GetObject(string poolName, Vector3 position, Quaternion rotation)
        {
            return GetPool(poolName).GetObject(position, rotation);
        }

        /// <summary>
        /// Gets an object from the specified pool, and then retrieves the specified node type using a cache to improve performance.
        /// Note: this should not be used if multiple nodes of the same type exist on the object, or if the node will be dynamically removed/added at runtime.
        /// </summary>
        /// <typeparam name="T">The node type to get.</typeparam>
        /// <param name="poolName">The name of the pool to get the node from.</param>
        /// <returns>The retrieved node.</returns>
        public T GetObjectComponent<T>(string poolName) where T : class
        {
            return GetPool(poolName).GetObjectComponent<T>();
        }

        /// <summary>
        /// Gets an object from the specified pool, and then retrieves the specified node type using a cache to improve performance.
        /// Note: this should not be used if multiple nodes of the same type exist on the object, or if the node will be dynamically removed/added at runtime.
        /// </summary>
        /// <typeparam name="T">The node type to get.</typeparam>
        /// <param name="poolName">The name of the pool to get the node from.</param>
        /// <param name="position">The position to set the object to.</param>
        /// <returns>The retrieved node.</returns>
        public T GetObjectComponent<T>(string poolName, Vector3 position) where T : class
        {
            return GetPool(poolName).GetObjectComponent<T>(position);
        }

        /// <summary>
        /// Gets an object from the specified pool, and then retrieves the specified node type using a cache to improve performance.
        /// Note: this should not be used if multiple nodes of the same type exist on the object, or if the node will be dynamically removed/added at runtime.
        /// </summary>
        /// <typeparam name="T">The node type to get.</typeparam>
        /// <param name="poolName">The name of the pool to get the node from.</param>
        /// <param name="position">The position to set the object to.</param>
        /// <param name="rotation">The rotation to set the object to.</param>
        /// <returns>The retrieved node.</returns>
        public T GetObjectComponent<T>(string poolName, Vector3 position, Quaternion rotation) where T : class
        {
            return GetPool(poolName).GetObjectComponent<T>(position, rotation);
        }
        #endregion

        #region Release/Destroys
        /// <summary>
        /// Releases an object and returns it back to the specified pool, effectively 'destroying' it from the scene.
        /// Pool equivalent of QueueFree.
        /// </summary>
        /// <param name="obj">The object to release.</param>
        /// <param name="poolName">The name of the pool to return the object to.</param>
        public void Release(Node obj, string poolName)
        {
            GetPool(poolName).Release(obj);
        }

        /// <summary>
        /// Releases a collection of objects and returns them back to the specified pool, effectively 'destroying' them from the scene.
        /// </summary>
        /// <param name="objs">the objects to release.</param>
        /// <param name="poolName">The name of the pool to return the objects to.</param>
        public void Release(IEnumerable<Node> objs, string poolName)
        {
            GetPool(poolName).Release(objs);
        }

        /// <summary>
        /// Releases every active object in the specified pool.
        /// </summary>
        /// <param name="poolName">The name of the pool.</param>
        public void ReleaseAll(string poolName)
        {
            GetPool(poolName).ReleaseAll();
        }

        /// <summary>
        /// Forcibly destroys the object and does not return it to a pool.
        /// </summary>
        /// <param name="obj">The object to destroy.</param>
        public void Destroy(Node obj) { Destroy(obj, obj.Name); }

        /// <summary>
        /// Forcibly destroys the object and does not return it to a pool.
        /// </summary>
        /// <param name="obj">The object to destroy.</param>
        /// <param name="poolName">The name of the pool that the object belonged to.</param>
        public void Destroy(Node obj, string poolName)
        {
            if (_poolTable.TryGetValue(poolName, out ObjectPool pool)) { pool.Destroy(obj); }
            else if (GodotObject.IsInstanceValid(obj)) { obj.QueueFree(); }
        }

        /// <summary>
        /// Forcibly destroys a collection of objects and does not return them to a pool.
        /// </summary>
        /// <param name="objs">The objects to destroy.</param>
        /// <param name="poolName">The name of the pool that the objects belonged to.</param>
        public void Destroy(IEnumerable<Node> objs, string poolName)
        {
            if (_poolTable.TryGetValue(poolName, out ObjectPool pool)) { pool.Destroy(objs); }
            else
            {
                foreach (Node obj in objs)
                {
                    if (GodotObject.IsInstanceValid(obj)) { obj.QueueFree(); }
                }
            }
        }

        /// <summary>
        /// Releases every active object in every pool.
        /// </summary>
        public void ReleaseAllInAllPools()
        {
            foreach (ObjectPool pool in _poolTable.Values)
            {
                pool.ReleaseAll();
            }
        }
        #endregion

        #region Miscellaneous
        /// <summary>
        /// Populates the specified pool with the specified number of objects, so that they do not need instantiating later.
        /// </summary>
        /// <param name="poolName">The name of the pool to populate.</param>
        /// <param name="quantity">The number of objects to populate it with.</param>
        /// <param name="method">The population mode.</param>
        public void Populate(string poolName, int quantity, PopulateMethod method = PopulateMethod.Set)
        {
            GetPool(poolName).Populate(quantity, method);
        }

        /// <summary>
        /// Destroys every object in the specified pool, both alive and pooled.
        /// </summary>
        /// <param name="poolName">The name of the pool to populate.</param>
        public void Purge(string poolName)
        {
            GetPool(poolName).Purge();
        }

        /// <summary>
        /// Destroys every object in every pool, both alive and pooled.
        /// </summary>
        public void PurgeAll()
        {
            foreach (ObjectPool pool in _poolTable.Values)
            {
                pool.Purge();
            }
        }

        /// <summary>
        /// Gets all active objects in the specified pool.
        /// </summary>
        /// <param name="poolName">The name of the pool to populate.</param>
        /// <returns>The active objects.</returns>
        public IEnumerable<Node> GetAllActiveObjects(string poolName)
        {
            return GetPool(poolName).GetAllActiveObjects();
        }
        #endregion
    }
}
