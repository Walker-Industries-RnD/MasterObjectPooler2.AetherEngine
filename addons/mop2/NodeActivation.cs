using Godot;

namespace QFSW.MOP2
{
    /// <summary>
    /// Helpers that emulate Unity's <c>GameObject.SetActive</c> semantics for Godot <see cref="Node"/>s.
    /// A deactivated node is hidden (when it is visual) and has its processing/input disabled, mirroring
    /// how a pooled Unity object is disabled while it waits in the pool.
    /// </summary>
    internal static class NodeActivation
    {
        /// <summary>
        /// Activates or deactivates a node. Deactivation hides visual nodes and stops
        /// <c>_Process</c>/<c>_PhysicsProcess</c>/input from firing, matching the behaviour of a pooled object.
        /// </summary>
        public static void SetActive(Node node, bool active)
        {
            if (node is Node3D node3D)
            {
                node3D.Visible = active;
            }
            else if (node is CanvasItem canvasItem)
            {
                canvasItem.Visible = active;
            }

            node.ProcessMode = active ? Node.ProcessModeEnum.Inherit : Node.ProcessModeEnum.Disabled;
        }
    }
}
