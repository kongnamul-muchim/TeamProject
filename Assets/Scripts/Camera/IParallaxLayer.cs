using UnityEngine;

namespace HideAndInk.Parallax
{
    public interface IParallaxLayer
    {
        float SpeedRatio { get; }
        void ApplyOffset(Vector3 delta);
    }
}
