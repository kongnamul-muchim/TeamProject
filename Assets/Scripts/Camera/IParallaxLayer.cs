using UnityEngine;

namespace HideAndInk.ParallaxSystem
{
    public interface IParallaxLayer
    {
        float SpeedRatio { get; }
        void ApplyOffset(Vector3 delta);
    }
}
