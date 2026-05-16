// Pcx - Point cloud importer & renderer for Unity
using UnityEngine;
using System.Collections.Generic;

namespace Pcx {
    public sealed class PointCloudData : ScriptableObject {
        // STRICT ALIGNMENT: 4 Vector4s = 16 floats = 64 bytes
        public const int elementSize = sizeof(float) * 16;

        public int pointCount { get { return _pointData.Length; } }

        public ComputeBuffer computeBuffer {
            get {
                if (_pointBuffer == null) {
                    _pointBuffer = new ComputeBuffer(pointCount, elementSize);
                    _pointBuffer.SetData(_pointData);
                }
                return _pointBuffer;
            }
        }

        ComputeBuffer _pointBuffer;

        void OnDisable() {
            if (_pointBuffer != null) { _pointBuffer.Release(); _pointBuffer = null; }
        }

        [System.Serializable]
        struct SplatData {
            public Vector4 position;
            public Vector4 rotation;
            public Vector4 scale;
            public Vector4 color;
        }

        [SerializeField] SplatData[] _pointData;

#if UNITY_EDITOR
        public void Initialize(List<Vector3> positions, List<Color> colors, List<Vector2> scales, List<Vector4> rotations, List<float> opacities) {
            _pointData = new SplatData[positions.Count];
            for (var i = 0; i < _pointData.Length; i++) {
                // Normalize rotation
                Vector4 rot = rotations[i];
                float rotLen = Mathf.Sqrt(rot.x * rot.x + rot.y * rot.y + rot.z * rot.z + rot.w * rot.w);
                if (rotLen > 0.0001f) rot /= rotLen;

                // 2DGS Opacity uses a Sigmoid function to get the real alpha (0 to 1)
                float alpha = 1.0f / (1.0f + Mathf.Exp(-opacities[i]));

                _pointData[i] = new SplatData {
                    position = new Vector4(positions[i].x, positions[i].y, positions[i].z, 1.0f),
                    rotation = rot,
                    scale = new Vector4(Mathf.Exp(scales[i].x), Mathf.Exp(scales[i].y), 1.0f, 1.0f),
                    color = new Vector4(colors[i].r, colors[i].g, colors[i].b, alpha)
                };
            }
        }
#endif
    }
}