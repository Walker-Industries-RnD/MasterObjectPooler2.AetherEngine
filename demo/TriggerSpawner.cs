using Godot;

namespace QFSW.MOP2.Demo
{
    /// <summary>
    /// Continuously spawns pooled <see cref="RigidBody3D"/> objects at a fixed rate, launching each with a random
    /// linear and angular velocity.
    /// </summary>
    [GlobalClass]
    public partial class TriggerSpawner : Node3D
    {
        [Export]
        private ObjectPool _triggerPool = null;

        [Export]
        private float _spawnRate = 2;

        [Export]
        private float _spawnSpeed = 3;

        [Export]
        private float _spawnAngularSpeed = 6;

        private bool ShouldSpawn => Time.GetTicksMsec() / 1000.0 > _lastSpawned + 1.0 / _spawnRate;

        private double _lastSpawned;

        public override void _Ready()
        {
            _triggerPool.Initialize();
            _triggerPool.ObjectParent.Reparent(this, keepGlobalTransform: false);
        }

        public override void _Process(double delta)
        {
            if (ShouldSpawn)
            {
                _lastSpawned = Time.GetTicksMsec() / 1000.0;
                RigidBody3D rb = _triggerPool.GetObjectComponent<RigidBody3D>(GlobalPosition);
                rb.AngularVelocity = RandomVector() * _spawnAngularSpeed;
                rb.LinearVelocity = RandomVector() * _spawnSpeed;
            }
        }

        private static Vector3 RandomVector()
        {
            return new Vector3(
                (float)GD.RandRange(-1.0, 1.0),
                (float)GD.RandRange(-1.0, 1.0),
                (float)GD.RandRange(-1.0, 1.0));
        }
    }
}
