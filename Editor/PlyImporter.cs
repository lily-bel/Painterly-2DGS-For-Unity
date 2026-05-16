// Pcx - Point cloud importer & renderer for Unity
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;

namespace Pcx {
    // Version 3 forces Unity to re-import and show our new UI!
    [UnityEditor.AssetImporters.ScriptedImporter(3, "ply")]
    class PlyImporter : UnityEditor.AssetImporters.ScriptedImporter {
        public enum ContainerType { Mesh, ComputeBuffer, Texture }
        [SerializeField] ContainerType _containerType = ContainerType.ComputeBuffer;

        [Header("Import Optimization (Permanently deletes data)")]

        [Tooltip("Randomly drop this percentage of splats (e.g. 0.5 = Keep half). Great for heavy decimation.")]
        [Range(0f, 0.99f)]
        public float decimation = 0.0f;

        [Tooltip("Drop splats with an opacity lower than this. (0.05 = 5% visible)")]
        [Range(0f, 1f)]
        public float cullOpacity = 0.05f;

        [Tooltip("Drop splats smaller than this Logarithmic scale (e.g., -6 is tiny dust, 0 is huge)")]
        public float cullScale = -6.0f;

        public override void OnImportAsset(UnityEditor.AssetImporters.AssetImportContext context) {
            if (_containerType == ContainerType.ComputeBuffer) {
                var gameObject = new GameObject();
                var data = ImportAsPointCloudData(context.assetPath);
                if (data != null) {
                    var renderer = gameObject.AddComponent<PointCloudRenderer>();
                    renderer.sourceData = data;
                    context.AddObjectToAsset("prefab", gameObject);
                    context.AddObjectToAsset("data", data);
                    context.SetMainObject(gameObject);
                }
            }
        }

        enum PropType { Float, Double, UChar, Char, UShort, Short, UInt, Int }

        class PlyProperty {
            public string name;
            public PropType type;
        }

        PointCloudData ImportAsPointCloudData(string path) {
            try {
                using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new BinaryReader(stream)) {
                    var header = ReadDataHeader(reader);
                    var body = ReadDataBody(header, reader);
                    var data = ScriptableObject.CreateInstance<PointCloudData>();
                    data.Initialize(body.vertices, body.colors, body.scales, body.rotations, body.opacities);
                    data.name = Path.GetFileNameWithoutExtension(path);

                    // Print a helpful log showing how many points we saved!
                    Debug.Log($"[2DGS Optimizer] Imported {data.name}: Kept {body.vertices.Count:N0} points (Original: {header.vertexCount:N0}).");

                    return data;
                }
            } catch (Exception e) { Debug.LogError("Failed importing " + path + ". " + e.Message); return null; }
        }

        class DataHeader {
            public List<PlyProperty> properties = new List<PlyProperty>();
            public int vertexCount = -1;
        }

        class DataBody {
            public List<Vector3> vertices = new List<Vector3>();
            public List<Color> colors = new List<Color>();
            public List<Vector2> scales = new List<Vector2>();
            public List<Vector4> rotations = new List<Vector4>();
            public List<float> opacities = new List<float>();
        }

        DataHeader ReadDataHeader(BinaryReader reader) {
            var data = new DataHeader();
            var line = ReadLine(reader);
            if (line != "ply") throw new ArgumentException("Magic number mismatch.");
            ReadLine(reader); // format

            while (true) {
                line = ReadLine(reader);
                if (line == "end_header") break;
                var col = line.Split();

                if (col[0] == "element" && col[1] == "vertex") {
                    data.vertexCount = Convert.ToInt32(col[2]);
                } else if (col[0] == "property") {
                    var prop = new PlyProperty { name = col[2] };
                    switch (col[1]) {
                        case "float": case "float32": prop.type = PropType.Float; break;
                        case "double": case "float64": prop.type = PropType.Double; break;
                        case "uchar": case "uint8": prop.type = PropType.UChar; break;
                        case "char": case "int8": prop.type = PropType.Char; break;
                        case "ushort": case "uint16": prop.type = PropType.UShort; break;
                        case "short": case "int16": prop.type = PropType.Short; break;
                        case "uint": case "uint32": prop.type = PropType.UInt; break;
                        case "int": case "int32": prop.type = PropType.Int; break;
                        default: continue;
                    }
                    data.properties.Add(prop);
                }
            }
            return data;
        }

        string ReadLine(BinaryReader reader) {
            var bytes = new List<byte>();
            while (true) {
                var b = reader.ReadByte();
                if (b == '\n') break;
                if (b != '\r') bytes.Add(b);
            }
            return System.Text.Encoding.ASCII.GetString(bytes.ToArray());
        }

        DataBody ReadDataBody(DataHeader header, BinaryReader reader) {
            var data = new DataBody();
            for (var i = 0; i < header.vertexCount; i++) {
                float x = 0, y = 0, z = 0;
                float r = 1, g = 1, b = 1;
                float s0 = -5f, s1 = -5f;
                float rot0 = 1f, rot1 = 0, rot2 = 0, rot3 = 0;
                float fdc0 = 0, fdc1 = 0, fdc2 = 0;
                float opacity = 2f;
                bool hasColor = false, hasFdc = false;

                foreach (var prop in header.properties) {
                    float val = 0;
                    switch (prop.type) {
                        case PropType.Float: val = reader.ReadSingle(); break;
                        case PropType.Double: val = (float)reader.ReadDouble(); break;
                        case PropType.UChar: val = reader.ReadByte(); break;
                        case PropType.Char: val = reader.ReadSByte(); break;
                        case PropType.UShort: val = reader.ReadUInt16(); break;
                        case PropType.Short: val = reader.ReadInt16(); break;
                        case PropType.UInt: val = reader.ReadUInt32(); break;
                        case PropType.Int: val = reader.ReadInt32(); break;
                    }

                    switch (prop.name) {
                        case "x": x = val; break;
                        case "y": y = val; break;
                        case "z": z = val; break;
                        case "red": r = val / 255f; hasColor = true; break;
                        case "green": g = val / 255f; hasColor = true; break;
                        case "blue": b = val / 255f; hasColor = true; break;
                        case "scale_0": case "sx": s0 = val; break;
                        case "scale_1": case "sy": s1 = val; break;
                        case "rot_0": case "qw": case "quat_0": rot0 = val; break;
                        case "rot_1": case "qx": case "quat_1": rot1 = val; break;
                        case "rot_2": case "qy": case "quat_2": rot2 = val; break;
                        case "rot_3": case "qz": case "quat_3": rot3 = val; break;
                        case "f_dc_0": fdc0 = val; hasFdc = true; break;
                        case "f_dc_1": fdc1 = val; hasFdc = true; break;
                        case "f_dc_2": fdc2 = val; hasFdc = true; break;
                        case "opacity": case "alpha": opacity = val; break;
                    }
                }

                // --- OUR NEW CULLING LOGIC ---
                // Calculate true opacity using the Sigmoid function (0.0 to 1.0)
                float trueOpacity = 1.0f / (1.0f + Mathf.Exp(-opacity));

                // 1. Transparency Culling
                if (trueOpacity < cullOpacity) continue;

                // 2. Micro-Dust Culling
                if (s0 < cullScale && s1 < cullScale) continue;

                // 3. Brute Force Decimation
                if (decimation > 0.0f && UnityEngine.Random.value < decimation) continue;
                // -----------------------------

                if (hasFdc && !hasColor) {
                    r = Mathf.Clamp01(0.5f + 0.28209f * fdc0);
                    g = Mathf.Clamp01(0.5f + 0.28209f * fdc1);
                    b = Mathf.Clamp01(0.5f + 0.28209f * fdc2);
                }

                data.vertices.Add(new Vector3(x, y, z));
                data.colors.Add(new Color(r, g, b));
                data.scales.Add(new Vector2(s0, s1));
                data.rotations.Add(new Vector4(rot1, rot2, rot3, rot0));
                data.opacities.Add(opacity);
            }
            return data;
        }
    }
}