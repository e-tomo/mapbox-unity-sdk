namespace Mapbox.Unity.MeshGeneration.Modifiers
{
    using System.Collections.Generic;
    using UnityEngine;
    using Mapbox.Unity.MeshGeneration.Data;
	using System;

	public enum ExtrusionType
    {
        Wall,
        FirstMidFloor,
        FirstMidTopFloor
    }

    /// <summary>
    /// Height Modifier is responsible for the y axis placement of the feature. It pushes the original vertices upwards by "height" value and creates side walls around that new polygon down to "min_height" value.
    /// It also checkes for "ele" (elevation) value used for contour lines in Mapbox Terrain data. 
    /// Height Modifier also creates a continuous UV mapping for side walls.
    /// </summary>
    [CreateAssetMenu(menuName = "Mapbox/Modifiers/Height Modifier")]
    public class HeightModifier : MeshModifier
    {
        [SerializeField]
		[Tooltip("Flatten top polygons to prevent unwanted slanted roofs because of the bumpy terrain")]
        private bool _flatTops;
        
        [SerializeField]
		[Tooltip("Fix all features to certain height, suggested to be used for pushing roads above terrain level to prevent z-fighting.")]
		private bool _forceHeight;

		[SerializeField]
		[Tooltip("Fixed height value for ForceHeight option")]
		private float _height;
		private float _scale = 1;

		[SerializeField]
		private string _heightPropertyName;

		[SerializeField]
		private int maxHeight;

		[SerializeField]
		private float _scaleCoefficient;

		[SerializeField]
		[Tooltip("Create side walls from calculated height down to terrain level. Suggested for buildings, not suggested for roads.")]
		private bool _createSideWalls = true;

        public override ModifierType Type { get { return ModifierType.Preprocess; } }

		[NonSerialized] private int _counter;


		public override void Run(VectorFeatureUnity feature, MeshData md, float scale)
		{
			_scale = scale;
			Run(feature, md);
		}

		public override void Run(VectorFeatureUnity feature, MeshData md, UnityTile tile = null)
        {
            if (md.Vertices.Count == 0 || feature == null || feature.Points.Count < 1)
                return;

			if (tile != null)
				_scale = tile.TileScale;

			var minHeight = 0f;
            float hf = _height * _scale;
            if (!_forceHeight)
            {
				if (feature.Properties.ContainsKey(_heightPropertyName))
				{
					if (float.TryParse(feature.Properties[_heightPropertyName].ToString(), out hf))
					{
						hf *= _scale * _scaleCoefficient;

						if (hf > maxHeight)
							hf = maxHeight;
					}
				}
                if (feature.Properties.ContainsKey("height"))
                {
                    if (float.TryParse(feature.Properties["height"].ToString(), out hf))
                    {
						hf *= _scale;
                        if (feature.Properties.ContainsKey("min_height"))
                        {
                            minHeight = float.Parse(feature.Properties["min_height"].ToString()) * _scale;
                            hf -= minHeight;
                        }
                    }
                }
                if (feature.Properties.ContainsKey("ele"))
                {
                    if (float.TryParse(feature.Properties["ele"].ToString(), out hf))
                    {
						hf *= _scale;
                    }
                }
            }

            var max = md.Vertices[0].y;
            var min = md.Vertices[0].y;
			_counter = md.Vertices.Count;
			if (_flatTops)
            {
				for (int i = 0; i < _counter; i++)
                {
                    if (md.Vertices[i].y > max)
                        max = md.Vertices[i].y;
                    else if (md.Vertices[i].y < min)
                        min = md.Vertices[i].y;
                }
                for (int i = 0; i < _counter; i++)
                {
                    md.Vertices[i] = new Vector3(md.Vertices[i].x, max + minHeight + hf, md.Vertices[i].z);
                }
                hf += max - min;
            }
            else
            {
                for (int i = 0; i < _counter; i++)
                {
                    md.Vertices[i] = new Vector3(md.Vertices[i].x, md.Vertices[i].y + minHeight + hf, md.Vertices[i].z);
                }
            }

			
			md.Vertices.Capacity = _counter + md.Edges.Count * 2;
			float d = 0f;
			Vector3 v1;
			Vector3 v2;
			int ind = 0;

			if (_createSideWalls)
			{
				_counter = md.Edges.Count;
				var wallTri = new List<int>(_counter * 3);
				var wallUv = new List<Vector2>(_counter * 2);
				Vector3 norm = Vector3.zero;
				for (int i = 0; i < _counter; i += 2)
				{
					v1 = md.Vertices[md.Edges[i]];
					v2 = md.Vertices[md.Edges[i + 1]];
					ind = md.Vertices.Count;
					md.Vertices.Add(v1);
					md.Vertices.Add(v2);
					md.Vertices.Add(new Vector3(v1.x, v1.y - hf, v1.z));
					md.Vertices.Add(new Vector3(v2.x, v2.y - hf, v2.z));

					d = (v2 - v1).magnitude;

					norm = Vector3.Cross(v2 - v1, md.Vertices[ind + 2] - v1).normalized;
					md.Normals.Add(norm);
					md.Normals.Add(norm);
					md.Normals.Add(norm);
					md.Normals.Add(norm);

					wallUv.Add(new Vector2(0, 0));
					wallUv.Add(new Vector2(d, 0));
					wallUv.Add(new Vector2(0, -hf));
					wallUv.Add(new Vector2(d, -hf));

					wallTri.Add(ind);
					wallTri.Add(ind + 1);
					wallTri.Add(ind + 2);

					wallTri.Add(ind + 1);
					wallTri.Add(ind + 3);
					wallTri.Add(ind + 2);
				}

				md.Triangles.Add(wallTri);
				md.UV[0].AddRange(wallUv);
			}
        }
    }
}
