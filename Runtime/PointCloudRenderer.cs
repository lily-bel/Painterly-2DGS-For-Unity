// Pcx - Point cloud importer & renderer for Unity
using UnityEngine;

namespace Pcx {
    [ExecuteInEditMode]
    public sealed class PointCloudRenderer : MonoBehaviour {
        [Header("Gaussian Splat Stylization")]
        [SerializeField] Material _customSplatMaterial = null;
        public Material customSplatMaterial {
            get { return _customSplatMaterial; }
            set { _customSplatMaterial = value; }
        }

        [Header("Legacy Pcx Settings")]
        [SerializeField] PointCloudData _sourceData = null;
        public PointCloudData sourceData { get { return _sourceData; } set { _sourceData = value; } }
        [SerializeField] Color _pointTint = new Color(0.5f, 0.5f, 0.5f, 1);
        [SerializeField] float _pointSize = 0.05f;

        public ComputeBuffer sourceBuffer { get; set; }

        [SerializeField, HideInInspector] Shader _pointShader = null;
        [SerializeField, HideInInspector] Shader _diskShader = null;
        Material _pointMaterial;
        Material _diskMaterial;

        void OnValidate() { _pointSize = Mathf.Max(0, _pointSize); }

        void OnDestroy() {
            if (_pointMaterial != null) {
                if (Application.isPlaying) { Destroy(_pointMaterial); Destroy(_diskMaterial); } else { DestroyImmediate(_pointMaterial); DestroyImmediate(_diskMaterial); }
            }
        }

        void OnRenderObject() {
            if (_sourceData == null && sourceBuffer == null) return;
            var camera = Camera.current;
            if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            if (camera.name == "Preview Scene Camera") return;

            var pointBuffer = sourceBuffer != null ? sourceBuffer : _sourceData.computeBuffer;

            // -------- OUR NEW CUSTOM MATERIAL INJECTION --------
            if (_customSplatMaterial != null) {
                _customSplatMaterial.SetMatrix("_Transform", transform.localToWorldMatrix);
                _customSplatMaterial.SetBuffer("_SplatBuffer", pointBuffer);

                // Loop through every pass in the shader (Pass 0: Depth, Pass 1: Color)
                for (int i = 0; i < _customSplatMaterial.passCount; i++) {
                    _customSplatMaterial.SetPass(i);

#if UNITY_2019_1_OR_NEWER
                    Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, pointBuffer.count);
#else
                    Graphics.DrawProceduralNow(MeshTopology.Triangles, 6, pointBuffer.count);
#endif
                }
                return; // Stop here, bypass the legacy Pcx renderer
            }

            // Lazy initialization for Legacy
            if (_pointMaterial == null) {
                _pointMaterial = new Material(_pointShader) { hideFlags = HideFlags.DontSave };
                _pointMaterial.EnableKeyword("_COMPUTE_BUFFER");
                _diskMaterial = new Material(_diskShader) { hideFlags = HideFlags.DontSave };
                _diskMaterial.EnableKeyword("_COMPUTE_BUFFER");
            }

            if (_pointSize == 0) {
                _pointMaterial.SetPass(0);
                _pointMaterial.SetColor("_Tint", _pointTint);
                _pointMaterial.SetMatrix("_Transform", transform.localToWorldMatrix);
                _pointMaterial.SetBuffer("_PointBuffer", pointBuffer);
                Graphics.DrawProceduralNow(MeshTopology.Points, pointBuffer.count, 1);
            } else {
                _diskMaterial.SetPass(0);
                _diskMaterial.SetColor("_Tint", _pointTint);
                _diskMaterial.SetMatrix("_Transform", transform.localToWorldMatrix);
                _diskMaterial.SetBuffer("_PointBuffer", pointBuffer);
                _diskMaterial.SetFloat("_PointSize", _pointSize);
                Graphics.DrawProceduralNow(MeshTopology.Points, pointBuffer.count, 1);
            }
        }
    }
}