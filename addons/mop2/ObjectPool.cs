using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace QFSW.MOP2
{
    /// <summary>
    /// Object pool containing several copies of a template scene (a <see cref="PackedScene"/>). Using the pool with GetObject and Release
    /// provides a high speed alternative to repeatedly calling PackedScene.Instantiate and Node.QueueFree.
    /// </summary>
    /// <remarks>
    /// Godot port of QFSW's Master Object Pooler 2. As Godot has no ScriptableObjects, the pool is a
    /// <see cref="Resource"/> and the pooled template is a <see cref="PackedScene"/> instead of a prefab GameObject.
    /// The pool is scene-tree aware: it lazily creates a holder node under the current scene (or under the owning
    /// <see cref="MasterObjectPooler"/>) and parents every pooled instance beneath it.
    /// </remarks>
    [GlobalClass]
    public partial class ObjectPool : Resource
    {
        [Export]
        private string _name = string.Empty;

        [Export]
        private PackedScene _template = null;

        [Export]
        private int _defaultSize;

        [Export]
        private int _maxSize = -1;

        [Export]
        private bool _incrementalInstanceNames = false;

        /// <summary>
        /// If enabled, object instances will be renamed to ObjectName#XXX where XXX is the instance number. This is useful if you want them all to be uniquely named.
        /// </summary>
        public bool IncrementalInstanceNames
        {
            get => _incrementalInstanceNames;
            set => _incrementalInstanceNames = value;
        }

        /// <summary>
        /// The name of the pool. Used for identification and as the key when using a MasterObjectPooler.
        /// </summary>
        public string PoolName => _name;

        /// <summary>
        /// Parent node for all pooled objects. Created lazily under the current scene the first time it is required.
        /// </summary>
        public Node ObjectParent
        {
            get
            {
                if (!GodotObject.IsInstanceValid(_objectParent))
                {
                    _objectParent = new Node3D { Name = $"{_name} Pool" };
                    SceneRoot.AddChild(_objectParent);
                }

                return _objectParent;
            }
        }
        private Node _objectParent;

        /// <summary>
        /// The node newly created holder nodes are parented under. Defaults to the active scene, falling back to the
        /// scene tree root. Overridden by a <see cref="MasterObjectPooler"/> when the pool is added to one.
        /// </summary>
        private static Node SceneRoot
        {
            get
            {
                SceneTree tree = (SceneTree)Engine.GetMainLoop();
                return GodotObject.IsInstanceValid(tree.CurrentScene) ? tree.CurrentScene : tree.Root;
            }
        }

        private bool HasMaxSize => _maxSize > 0;
        private bool HasPooledObjects => _pooledObjects.Count > 0;
        public bool Initialized { get; private set; }

        private int _instanceCounter = 0;

        // Local transform of the template, captured from the first instance that is created. Used to
        // reproduce Unity's behaviour of spawning objects at the template's transform when no override is given.
        private bool _templateCaptured = false;
        private Vector3 _templatePosition = Vector3.Zero;
        private Quaternion _templateRotation = Quaternion.Identity;
        private Vector3 _templateScale = Vector3.One;

        #region Caches
        private readonly List<Node> _pooledObjects = new List<Node>();
        private readonly Dictionary<ulong, Node> _aliveObjects = new Dictionary<ulong, Node>();

        private readonly List<Node> _releaseAllBuffer = new List<Node>();
        private readonly Dictionary<(ulong id, Type type), object> _componentCache = new Dictionary<(ulong id, Type type), object>();
        #endregion

        #region Initialization/Creation
        /// <summary>
        /// Creates an ObjectPool.
        /// </summary>
        /// <param name="template">The template scene to center the pool on. All objects in the pool will be an instance of this scene.</param>
        /// <param name="defaultSize">The default number of objects to create in this pool when initializing it.</param>
        /// <param name="maxSize">The maximum number of objects that can be kept in this pool. If it is exceeded, objects will be destroyed instead of pooled when returned. Set to -1 for no limit.</param>
        /// <returns>The created ObjectPool.</returns>
        public static ObjectPool Create(PackedScene template, int defaultSize = 0, int maxSize = -1)
        {
            return Create(template, DefaultNameFor(template), defaultSize, maxSize);
        }

        /// <summary>
        /// Creates an ObjectPool.
        /// </summary>
        /// <param name="template">The template scene to center the pool on. All objects in the pool will be an instance of this scene.</param>
        /// <param name="name">The name of the pool. Used for identification and as the key when using a MasterObjectPooler.</param>
        /// <param name="defaultSize">The default number of objects to create in this pool when initializing it.</param>
        /// <param name="maxSize">The maximum number of objects that can be kept in this pool. If it is exceeded, objects will be destroyed instead of pooled when returned. Set to -1 for no limit.</param>
        /// <returns>The created ObjectPool.</returns>
        public static ObjectPool Create(PackedScene template, string name, int defaultSize = 0, int maxSize = -1)
        {
            ObjectPool pool = new ObjectPool
            {
                _name = name,
                _template = template,
                _defaultSize = defaultSize,
                _maxSize = maxSize
            };

            return pool;
        }

        /// <summary>
        /// Creates an ObjectPool and initializes it.
        /// </summary>
        /// <param name="template">The template scene to center the pool on. All objects in the pool will be an instance of this scene.</param>
        /// <param name="defaultSize">The default number of objects to create in this pool when initializing it.</param>
        /// <param name="maxSize">The maximum number of objects that can be kept in this pool. If it is exceeded, objects will be destroyed instead of pooled when returned. Set to -1 for no limit.</param>
        /// <returns>The created ObjectPool.</returns>
        public static ObjectPool CreateAndInitialize(PackedScene template, int defaultSize = 0, int maxSize = -1)
        {
            ObjectPool pool = Create(template, defaultSize, maxSize);
            pool.Initialize();

            return pool;
        }

        /// <summary>
        /// Creates an ObjectPool and initializes it.
        /// </summary>
        /// <param name="template">The template scene to center the pool on. All objects in the pool will be an instance of this scene.</param>
        /// <param name="name">The name of the pool. Used for identification and as the key when using a MasterObjectPooler.</param>
        /// <param name="defaultSize">The default number of objects to create in this pool when initializing it.</param>
        /// <param name="maxSize">The maximum number of objects that can be kept in this pool. If it is exceeded, objects will be destroyed instead of pooled when returned. Set to -1 for no limit.</param>
        /// <returns>The created ObjectPool.</returns>
        public static ObjectPool CreateAndInitialize(PackedScene template, string name, int defaultSize = 0, int maxSize = -1)
        {
            ObjectPool pool = Create(template, name, defaultSize, maxSize);
            pool.Initialize();

            return pool;
        }

        /// <summary>
        /// Initializes the ObjectPool.
        /// </summary>
        public void Initialize(bool forceReinitialization = false)
        {
            if (!Initialized || forceReinitialization)
            {
                Initialized = true;

                AutoFillName();
                Populate(_defaultSize, PopulateMethod.Set);
            }
        }

        internal void AutoFillName()
        {
            if (string.IsNullOrWhiteSpace(_name))
            {
                _name = DefaultNameFor(_template);
                ObjectParent.Name = _name;
            }
        }

        private static string DefaultNameFor(PackedScene template)
        {
            if (template == null)
            {
                return "Pool";
            }

            // Use the resource file name (without extension) as a friendly default, mirroring Unity using the prefab name.
            string path = template.ResourcePath;
            if (string.IsNullOrEmpty(path))
            {
                return "Pool";
            }

            return System.IO.Path.GetFileNameWithoutExtension(path);
        }
        #endregion

        #region Internal
        private Node CreateNewObject() { return CreateNewObject(_templatePosition, _templateRotation); }
        private Node CreateNewObject(Vector3 position, Quaternion rotation)
        {
            Node newObj = _template.Instantiate();
            ObjectParent.AddChild(newObj);

            CaptureTemplateTransform(newObj);
            ApplyTransform(newObj, position, rotation, _templateScale, global: false);

            if (_incrementalInstanceNames)
            {
                newObj.Name = string.Format("{0}#{1:000}", DefaultNameFor(_template), _instanceCounter);
            }
            else
            {
                newObj.Name = DefaultNameFor(_template);
            }

            InitializePoolables(newObj);

            _instanceCounter++;
            return newObj;
        }

        private void CaptureTemplateTransform(Node instance)
        {
            if (!_templateCaptured && instance is Node3D node3D)
            {
                _templatePosition = node3D.Position;
                _templateRotation = node3D.Quaternion;
                _templateScale = node3D.Scale;
                _templateCaptured = true;
            }
        }

        private static void ApplyTransform(Node obj, Vector3 position, Quaternion rotation, Vector3 scale, bool global)
        {
            if (obj is Node3D node3D)
            {
                if (global && node3D.IsInsideTree())
                {
                    node3D.GlobalTransform = new Transform3D(new Basis(rotation), position);
                }
                else
                {
                    node3D.Position = position;
                    node3D.Quaternion = rotation;
                }

                node3D.Scale = scale;
            }
        }

        private void InitializePoolables(Node instance)
        {
            foreach (IPoolable poolable in FindPoolables(instance))
            {
                poolable.InitializeTemplate(this);
            }
        }

        private static IEnumerable<IPoolable> FindPoolables(Node root)
        {
            if (root is IPoolable rootPoolable)
            {
                yield return rootPoolable;
            }

            foreach (Node child in root.GetChildren())
            {
                foreach (IPoolable descendant in FindPoolables(child))
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// Removes references to any pooled objects that have been destroyed externally.
        /// The MOP2 equivalent of internal cleansing after a scene unload.
        /// </summary>
        public void Cleanse()
        {
            if (!GodotObject.IsInstanceValid(_objectParent))
            {
                _pooledObjects.Clear();
                _aliveObjects.Clear();
                _componentCache.Clear();
            }
            else
            {
                _pooledObjects.RemoveAll(x => !GodotObject.IsInstanceValid(x));
            }
        }
        #endregion

        #region GetObject/Component
        /// <summary>
        /// Gets an object from the pool.
        /// </summary>
        /// <returns>The retrieved object.</returns>
        public Node GetObject() { return GetObject(_templatePosition); }

        /// <summary>
        /// Gets an object from the pool.
        /// </summary>
        /// <param name="position">The position to set the object to.</param>
        /// <returns>The retrieved object.</returns>
        public Node GetObject(Vector3 position) { return GetObject(position, _templateRotation); }

        /// <summary>
        /// Gets an object from the pool.
        /// </summary>
        /// <param name="position">The position to set the object to.</param>
        /// <param name="rotation">The rotation to set the object to.</param>
        /// <returns>The retrieved object.</returns>
        public Node GetObject(Vector3 position, Quaternion rotation)
        {
            Node obj;
            if (HasPooledObjects)
            {
                obj = _pooledObjects[_pooledObjects.Count - 1];
                _pooledObjects.RemoveAt(_pooledObjects.Count - 1);

                if (!GodotObject.IsInstanceValid(obj))
                {
                    GD.PushWarning($"Object in pool '{_name}' was null or freed; it may have been destroyed externally. Attempting to retrieve a new object");
                    return GetObject(position, rotation);
                }

                ApplyTransform(obj, position, rotation, _templateScale, global: true);
            }
            else
            {
                obj = CreateNewObject(position, rotation);
            }

            NodeActivation.SetActive(obj, true);

            _aliveObjects.Add(obj.GetInstanceId(), obj);
            return obj;
        }

        /// <summary>
        /// Gets an object from the pool, and then retrieves the specified node type using a cache to improve performance.
        /// </summary>
        /// <typeparam name="T">The node type to get.</typeparam>
        /// <returns>The retrieved node.</returns>
        public T GetObjectComponent<T>() where T : class
        {
            return GetObjectComponent<T>(_templatePosition);
        }

        /// <summary>
        /// Gets an object from the pool, and then retrieves the specified node type using a cache to improve performance.
        /// </summary>
        /// <typeparam name="T">The node type to get.</typeparam>
        /// <param name="position">The position to set the object to.</param>
        /// <returns>The retrieved node.</returns>
        public T GetObjectComponent<T>(Vector3 position) where T : class
        {
            return GetObjectComponent<T>(position, _templateRotation);
        }

        /// <summary>
        /// Gets an object from the pool, and then retrieves the specified node type using a cache to improve performance.
        /// </summary>
        /// <typeparam name="T">The node type to get.</typeparam>
        /// <param name="position">The position to set the object to.</param>
        /// <param name="rotation">The rotation to set the object to.</param>
        /// <returns>The retrieved node.</returns>
        public T GetObjectComponent<T>(Vector3 position, Quaternion rotation) where T : class
        {
            Node obj = GetObject(position, rotation);
            return GetObjectComponent<T>(obj);
        }

        /// <summary>
        /// Retrieves the specified node type from an object using a cache to improve performance. The node itself
        /// is returned if it is of type <typeparamref name="T"/>, otherwise its first matching descendant is returned.
        /// </summary>
        /// <typeparam name="T">The node type to get.</typeparam>
        /// <param name="obj">The object to get the node from.</param>
        /// <returns>The retrieved node.</returns>
        public T GetObjectComponent<T>(Node obj) where T : class
        {
            (ulong id, Type type) key = (obj.GetInstanceId(), typeof(T));

            if (_componentCache.TryGetValue(key, out object cached))
            {
                if (cached is T cachedComponent && GodotObject.IsInstanceValid(cached as GodotObject))
                {
                    return cachedComponent;
                }

                _componentCache.Remove(key);
            }

            T component = FindComponent<T>(obj);
            if (component != null) { _componentCache[key] = component; }
            return component;
        }

        private static T FindComponent<T>(Node node) where T : class
        {
            if (node is T match)
            {
                return match;
            }

            foreach (Node child in node.GetChildren())
            {
                T found = FindComponent<T>(child);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
        #endregion

        #region Release/Destroy
        /// <summary>
        /// Releases an object and returns it back to the pool, effectively 'destroying' it from the scene.
        /// Pool equivalent of QueueFree.
        /// </summary>
        /// <param name="obj">The object to release.</param>
        public void Release(Node obj)
        {
            if (!_aliveObjects.Remove(obj.GetInstanceId()))
            {
                GD.PushWarning($"Object '{obj}' could not be found in pool '{_name}'; it may have already been released.");
                return;
            }

            if (GodotObject.IsInstanceValid(obj))
            {
                if (HasMaxSize && _pooledObjects.Count >= _maxSize)
                {
                    obj.QueueFree();
                }
                else
                {
                    _pooledObjects.Add(obj);
                    NodeActivation.SetActive(obj, false);
                    if (obj.GetParent() != ObjectParent)
                    {
                        if (obj.GetParent() != null) { obj.Reparent(ObjectParent, keepGlobalTransform: false); }
                        else { ObjectParent.AddChild(obj); }
                    }
                }
            }
        }

        /// <summary>
        /// Releases a collection of objects and returns them back to the pool, effectively 'destroying' them from the scene.
        /// </summary>
        /// <param name="objs">the objects to release.</param>
        public void Release(IEnumerable<Node> objs)
        {
            foreach (Node obj in objs)
            {
                Release(obj);
            }
        }

        /// <summary>
        /// Releases every active object in this pool.
        /// </summary>
        public void ReleaseAll()
        {
            _releaseAllBuffer.Clear();
            _releaseAllBuffer.AddRange(_aliveObjects.Values);
            Release(_releaseAllBuffer);
        }

        /// <summary>
        /// Forcibly destroys the object and does not return it to the pool.
        /// </summary>
        /// <param name="obj">The object to destroy.</param>
        public void Destroy(Node obj)
        {
            _aliveObjects.Remove(obj.GetInstanceId());
            if (GodotObject.IsInstanceValid(obj)) { obj.QueueFree(); }
        }

        /// <summary>
        /// Forcibly destroys a collection of objects and does not return them to the pool.
        /// </summary>
        /// <param name="objs">The objects to destroy.</param>
        public void Destroy(IEnumerable<Node> objs)
        {
            foreach (Node obj in objs)
            {
                Destroy(obj);
            }
        }
        #endregion

        #region Miscellaneous
        /// <summary>
        /// Populates the pool with the specified number of objects, so that they do not need instantiating later.
        /// </summary>
        /// <param name="quantity">The number of objects to populate it with.</param>
        /// <param name="method">The population mode.</param>
        public void Populate(int quantity, PopulateMethod method = PopulateMethod.Set)
        {
            int newObjCount;
            switch (method)
            {
                case PopulateMethod.Set: newObjCount = quantity - _pooledObjects.Count; break;
                case PopulateMethod.Add: newObjCount = quantity; break;
                default: newObjCount = 0; break;
            }

            if (HasMaxSize) { newObjCount = Mathf.Min(newObjCount, _maxSize - _pooledObjects.Count); }
            if (newObjCount < 0) { newObjCount = 0; }

            for (int i = 0; i < newObjCount; i++)
            {
                Node newObj = CreateNewObject();
                NodeActivation.SetActive(newObj, false);
                _pooledObjects.Add(newObj);
            }
        }

        /// <summary>
        /// Destroys every object in the pool, both alive and pooled.
        /// </summary>
        public void Purge()
        {
            foreach (Node obj in _pooledObjects)
            {
                if (GodotObject.IsInstanceValid(obj)) { obj.QueueFree(); }
            }
            foreach (Node obj in _aliveObjects.Values)
            {
                if (GodotObject.IsInstanceValid(obj)) { obj.QueueFree(); }
            }
            _pooledObjects.Clear();
            _aliveObjects.Clear();
            _componentCache.Clear();
        }

        /// <summary>
        /// Gets all active objects in the pool.
        /// </summary>
        /// <returns>The active objects.</returns>
        public IEnumerable<Node> GetAllActiveObjects()
        {
            return _aliveObjects.Values
                .Where(GodotObject.IsInstanceValid);
        }
        #endregion
    }
}
