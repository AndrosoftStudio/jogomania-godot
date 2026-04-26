using Godot;

namespace Jogomania.Map
{
    public partial class CameraController : Camera2D
    {
        [Export] public float ZoomSpeed { get; set; } = 0.15f;
        [Export] public float MinZoom { get; set; } = 0.05f;
        [Export] public float MaxZoom { get; set; } = 10.0f;
        
        private bool _isDragging = false;

        public override void _UnhandledInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseBtnEvent)
            {
                if (mouseBtnEvent.ButtonIndex == MouseButton.Middle || mouseBtnEvent.ButtonIndex == MouseButton.Right)
                {
                    _isDragging = mouseBtnEvent.Pressed;
                }

                if (mouseBtnEvent.ButtonIndex == MouseButton.WheelUp && mouseBtnEvent.Pressed)
                {
                    Zoom *= (1.0f + ZoomSpeed);
                    Zoom = Zoom.Clamp(new Vector2(MinZoom, MinZoom), new Vector2(MaxZoom, MaxZoom));
                }
                else if (mouseBtnEvent.ButtonIndex == MouseButton.WheelDown && mouseBtnEvent.Pressed)
                {
                    Zoom *= (1.0f - ZoomSpeed);
                    Zoom = Zoom.Clamp(new Vector2(MinZoom, MinZoom), new Vector2(MaxZoom, MaxZoom));
                }
            }
            else if (@event is InputEventMouseMotion mouseMotionEvent && _isDragging)
            {
                Position -= mouseMotionEvent.Relative / Zoom;
            }
        }
    }
}
