using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NoHammerBuildMenu
{
    // Draws a square grid of thin lines around the build aim point. The grid is anchored to
    // world coordinates (vertices at multiples of the grid size), so it stays still while the
    // player moves their aim. On terrain the lines follow the ground height; on top of a
    // player-built piece (e.g. a floor) the grid is flat at that piece's surface height.
    internal class GridOverlay
    {
        private const float Radius = 8f;        // half-extent of the drawn grid
        private const float FadeStart = 5f;     // distance from grid centre where lines start to fade
        private const float RebuildDistance = 2f; // aim distance from grid centre that triggers a rebuild
        private const float Lift = 0.03f;       // height above the surface, avoids z-fighting
        private const float SegmentLength = 0.5f; // line subdivision so lines can follow terrain

        private GameObject _object;
        private Mesh _mesh;
        private bool _hasMesh;

        private float _builtSize;
        private Vector3 _builtCenter;
        private bool _builtFlat;
        private float _builtFlatY;
        private Color _builtColor;

        public void Show(Vector3 aim, float size, bool flat, float flatY, Color color)
        {
            if (!EnsureObject())
                return;

            Vector3 center = new Vector3(
                Mathf.Round(aim.x / size) * size,
                0f,
                Mathf.Round(aim.z / size) * size);

            Vector2 offset = new Vector2(aim.x - _builtCenter.x, aim.z - _builtCenter.z);
            bool stale = !_hasMesh
                || !Mathf.Approximately(size, _builtSize)
                || flat != _builtFlat
                || (flat && Mathf.Abs(flatY - _builtFlatY) > 0.01f)
                || color != _builtColor
                || offset.magnitude > RebuildDistance;

            if (stale)
                Rebuild(center, size, flat, flatY, color);

            _object.SetActive(true);
        }

        public void Hide()
        {
            if (_object != null && _object.activeSelf)
                _object.SetActive(false);
        }

        public void Destroy()
        {
            if (_object != null)
                Object.Destroy(_object);
            if (_mesh != null)
                Object.Destroy(_mesh);
            _object = null;
            _mesh = null;
            _hasMesh = false;
        }

        private bool EnsureObject()
        {
            if (_object != null)
                return true;

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return false;

            _mesh = new Mesh { name = "NoHammerBuildMenu_Grid", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();

            _object = new GameObject("NoHammerBuildMenu_Grid");
            Object.DontDestroyOnLoad(_object);
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;

            MeshRenderer renderer = _object.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = new Material(shader);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _object.SetActive(false);
            return true;
        }

        private void Rebuild(Vector3 center, float size, bool flat, float flatY, Color color)
        {
            int half = Mathf.FloorToInt(Radius / size);
            int segments = Mathf.CeilToInt(2f * Radius / SegmentLength);
            float step = 2f * Radius / segments;

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();

            for (int i = -half; i <= half; i++)
            {
                float line = i * size;
                for (int axis = 0; axis < 2; axis++)
                {
                    int first = vertices.Count;
                    for (int s = 0; s <= segments; s++)
                    {
                        float t = -Radius + s * step;
                        float x = center.x + (axis == 0 ? line : t);
                        float z = center.z + (axis == 0 ? t : line);

                        float dist = Mathf.Sqrt((axis == 0 ? line * line + t * t : t * t + line * line));
                        float fade = Mathf.Clamp01(1f - (dist - FadeStart) / (Radius - FadeStart));

                        float y = flat ? flatY : GroundHeight(x, z);
                        vertices.Add(new Vector3(x, y + Lift, z));
                        colors.Add(new Color(color.r, color.g, color.b, color.a * fade));

                        if (s > 0)
                        {
                            indices.Add(first + s - 1);
                            indices.Add(first + s);
                        }
                    }
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetIndices(indices, MeshTopology.Lines, 0);
            _mesh.RecalculateBounds();

            _hasMesh = true;
            _builtSize = size;
            _builtCenter = center;
            _builtFlat = flat;
            _builtFlatY = flatY;
            _builtColor = color;
        }

        private static float GroundHeight(float x, float z)
        {
            if (ZoneSystem.instance == null)
                return 0f;
            return ZoneSystem.instance.GetGroundHeight(new Vector3(x, 0f, z));
        }
    }
}
