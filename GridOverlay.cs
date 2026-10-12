using BepInEx.Configuration;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NoHammerBuildMenu
{
    // Draws a square grid of lines around the build aim point. The grid is anchored to
    // world coordinates (vertices at multiples of the grid size), so it stays still while the
    // player moves their aim. On terrain the lines follow the ground height; on top of a
    // player-built piece (e.g. a floor) the grid is flat at that piece's surface height.
    internal class GridOverlay
    {
        private static ConfigEntry<float> _radius;          // half-extent of the drawn grid
        private static ConfigEntry<float> _fadeStart;       // distance from grid centre where lines start to fade
        private static ConfigEntry<float> _rebuildDistance; // aim distance from grid centre that triggers a rebuild
        private static ConfigEntry<float> _lift;            // height above the surface, keeps lines clear of grass and z-fighting
        private static ConfigEntry<float> _segmentLength;   // line subdivision so lines can follow terrain
        private static ConfigEntry<float> _gridLineWidth;   // width of the regular grid lines
        private static ConfigEntry<float> _chunkLineWidth;  // width of the thick lines drawn on world chunk (zone) borders
        private static ConfigEntry<float> _chunkLineLift;   // slightly above the grid lines so they draw on top
        private static ConfigEntry<Color> _lineColor;
        private static ConfigEntry<Color> _chunkLineColor;

        private static float Radius => _radius.Value;
        // Fade must start inside the radius, otherwise the fade division is by zero or negative.
        private static float FadeStart => Mathf.Min(_fadeStart.Value, Radius - 0.01f);
        private static float RebuildDistance => _rebuildDistance.Value;
        private static float Lift => _lift.Value;
        private static float SegmentLength => _segmentLength.Value;
        private static float GridLineWidth => _gridLineWidth.Value;
        private static float ChunkLineWidth => _chunkLineWidth.Value;
        private static float ChunkLineLift => _chunkLineLift.Value;
        private static Color LineColor => _lineColor.Value;
        private static Color ChunkLineColor => _chunkLineColor.Value;

        public static void BindConfig(ConfigFile config)
        {
            const string section = "Grid Appearance";

            _radius = config.Bind(section, "Radius", 8f,
                new ConfigDescription("Half-extent in metres of the drawn grid around the aim point.",
                    new AcceptableValueRange<float>(2f, 32f)));
            _fadeStart = config.Bind(section, "Fade Start", 5f,
                new ConfigDescription("Distance in metres from the grid centre where lines start to fade out (kept below Radius).",
                    new AcceptableValueRange<float>(0f, 32f)));
            _rebuildDistance = config.Bind(section, "Rebuild Distance", 2f,
                new ConfigDescription("How far in metres the aim point can move from the grid centre before the grid is redrawn.",
                    new AcceptableValueRange<float>(0.25f, 16f)));
            _lift = config.Bind(section, "Lift", 0.12f,
                new ConfigDescription("Height in metres above the surface, keeps lines clear of grass and z-fighting.",
                    new AcceptableValueRange<float>(0f, 1f)));
            _segmentLength = config.Bind(section, "Segment Length", 0.5f,
                new ConfigDescription("Line subdivision in metres so lines can follow terrain. Smaller is smoother but heavier.",
                    new AcceptableValueRange<float>(0.1f, 4f)));
            _gridLineWidth = config.Bind(section, "Line Width", 0.06f,
                new ConfigDescription("Width in metres of the regular grid lines.",
                    new AcceptableValueRange<float>(0.01f, 1f)));
            _chunkLineWidth = config.Bind(section, "Chunk Line Width", 0.2f,
                new ConfigDescription("Width in metres of the thick lines drawn on world chunk (zone) borders.",
                    new AcceptableValueRange<float>(0.01f, 2f)));
            _chunkLineLift = config.Bind(section, "Chunk Line Lift", 0.13f,
                new ConfigDescription("Height in metres above the surface of chunk border lines; keep slightly above Lift so they draw on top.",
                    new AcceptableValueRange<float>(0f, 1f)));
            _lineColor = config.Bind(section, "Line Color", new Color(1f, 1f, 1f, 0.5f),
                "Colour (including alpha) of the regular grid lines.");
            _chunkLineColor = config.Bind(section, "Chunk Line Color", new Color(1f, 0.1f, 0.1f, 0.75f),
                "Colour (including alpha) of the world chunk border lines.");
        }

        private GameObject _object;
        private Mesh _mesh;
        private bool _hasMesh;
        private bool _warned;

        private float _builtSize;
        private Vector3 _builtCenter;
        private bool _builtFlat;
        private float _builtFlatY;

        public void Show(Vector3 aim, float size, bool flat)
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
                || (flat && Mathf.Abs(aim.y - _builtFlatY) > 0.01f)
                || offset.magnitude > RebuildDistance;

            if (stale)
                Rebuild(center, size, flat, aim.y);

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

            Shader shader = Shader.Find("Sprites/Default")
                ?? Shader.Find("Legacy Shaders/Particles/Alpha Blended")
                ?? Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                if (!_warned)
                    Debug.LogWarning("NoHammerBuildMenu: no usable shader found for the build grid overlay.");
                _warned = true;
                return false;
            }

            _mesh = new Mesh { name = "NoHammerBuildMenu_Grid", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();

            _object = new GameObject("NoHammerBuildMenu_Grid");
            Object.DontDestroyOnLoad(_object);
            _object.AddComponent<MeshFilter>().sharedMesh = _mesh;

            MeshRenderer renderer = _object.AddComponent<MeshRenderer>();
            Material material = new Material(shader);
            // Submesh 0 holds the grid line ribbons, submesh 1 the thick chunk border ribbons.
            renderer.sharedMaterials = new[] { material, material };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            _object.SetActive(false);
            return true;
        }

        private void Rebuild(Vector3 center, float size, bool flat, float flatY)
        {
            int half = Mathf.FloorToInt(Radius / size);
            int segments = Mathf.CeilToInt(2f * Radius / SegmentLength);
            float step = 2f * Radius / segments;

            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var indices = new List<int>();
            var chunkIndices = new List<int>();

            for (int i = -half; i <= half; i++)
            {
                float line = i * size;
                AddRibbon(0, center.x + line, center.z, line, GridLineWidth, Lift, LineColor, flat, flatY, segments, step, vertices, colors, indices);
                AddRibbon(1, center.z + line, center.x, line, GridLineWidth, Lift, LineColor, flat, flatY, segments, step, vertices, colors, indices);
            }

            AddChunkLines(center, flat, flatY, segments, step, vertices, colors, chunkIndices);

            _mesh.Clear();
            _mesh.subMeshCount = 2;
            _mesh.SetVertices(vertices);
            _mesh.SetColors(colors);
            _mesh.SetIndices(indices, MeshTopology.Triangles, 0);
            _mesh.SetIndices(chunkIndices, MeshTopology.Triangles, 1);
            _mesh.RecalculateBounds();

            _hasMesh = true;
            _builtSize = size;
            _builtCenter = center;
            _builtFlat = flat;
            _builtFlatY = flatY;
        }

        // Draws a thick ribbon along every world chunk (zone) border within the grid's radius.
        // Zone borders sit half a zone size from the zone centres, i.e. at 32 + 64k.
        private static void AddChunkLines(Vector3 center, bool flat, float flatY, int segments, float step,
            List<Vector3> vertices, List<Color> colors, List<int> indices)
        {
            const float zoneSize = ZoneSystem.c_ZoneSize;
            const float halfZone = zoneSize / 2f;

            for (int axis = 0; axis < 2; axis++)
            {
                float centerAlong = axis == 0 ? center.x : center.z;
                float centerAcross = axis == 0 ? center.z : center.x;

                int first = Mathf.CeilToInt((centerAlong - Radius - halfZone) / zoneSize);
                int last = Mathf.FloorToInt((centerAlong + Radius - halfZone) / zoneSize);

                for (int k = first; k <= last; k++)
                {
                    float line = k * zoneSize + halfZone;
                    AddRibbon(axis, line, centerAcross, line - centerAlong, ChunkLineWidth, ChunkLineLift,
                        ChunkLineColor, flat, flatY, segments, step, vertices, colors, indices);
                }
            }
        }

        // Adds a flat ribbon running along the other axis at `line` (a world x for axis 0, z for axis 1),
        // spanning the grid radius around `centerAcross`. `lineOffset` is the line's distance from the
        // grid centre, used for the distance fade.
        private static void AddRibbon(int axis, float line, float centerAcross, float lineOffset, float width, float lift,
            Color baseColor, bool flat, float flatY, int segments, float step,
            List<Vector3> vertices, List<Color> colors, List<int> indices)
        {
            float halfWidth = width / 2f;
            int start = vertices.Count;

            for (int s = 0; s <= segments; s++)
            {
                float t = -Radius + s * step;
                float dist = Mathf.Sqrt(lineOffset * lineOffset + t * t);
                float fade = Mathf.Clamp01(1f - (dist - FadeStart) / (Radius - FadeStart));
                Color color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * fade);

                for (int side = -1; side <= 1; side += 2)
                {
                    float along = line + side * halfWidth;
                    float across = centerAcross + t;
                    float x = axis == 0 ? along : across;
                    float z = axis == 0 ? across : along;
                    float y = flat ? flatY : GroundHeight(x, z);
                    vertices.Add(new Vector3(x, y + lift, z));
                    colors.Add(color);
                }

                if (s > 0)
                {
                    int a = start + (s - 1) * 2;
                    indices.Add(a); indices.Add(a + 1); indices.Add(a + 2);
                    indices.Add(a + 1); indices.Add(a + 3); indices.Add(a + 2);
                }
            }
        }

        private static float GroundHeight(float x, float z)
        {
            if (ZoneSystem.instance == null)
                return 0f;
            return ZoneSystem.instance.GetGroundHeight(new Vector3(x, 0f, z));
        }
    }
}
