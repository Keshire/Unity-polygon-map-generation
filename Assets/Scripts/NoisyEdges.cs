using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using graphs;

public class NoisyEdges {

	public static float NOISY_LINE_TRADEOFF = 0.5F;
	public Dictionary<int, List<Vector2>> path0 = new Dictionary<int, List<Vector2>>();
	public Dictionary<int, List<Vector2>> path1 = new Dictionary<int, List<Vector2>>();
	public static List<Vector2> points;// = new List<Vector2> ();


	public NoisyEdges(){}


	// Build noisy line paths for each of the Voronoi edges. There are
	// two noisy line paths for each edge, each covering half the
	// distance: path0 is from v0 to the midpoint and path1 is from v1
	// to the midpoint. When drawing the polygons, one or the other
	// must be drawn in reverse order.	
	public void buildNoisyEdges(Map map, Lava lava, System.Random random){

		foreach (var p in map.centers) {
			foreach (var edge in p.borders) {
				if ((edge.d0 !=null && edge.d1 !=null) && (edge.v0 !=null && edge.v1 !=null) && !path0.ContainsKey(edge.index)) {

					var f = NOISY_LINE_TRADEOFF;
					var t = Vector2.Lerp (edge.v0.point, edge.d0.point, f);
					var q = Vector2.Lerp (edge.v0.point, edge.d1.point, f);
					var r = Vector2.Lerp (edge.v1.point, edge.d0.point, f);
					var s = Vector2.Lerp (edge.v1.point, edge.d1.point, f);
					
					var minLength = 10;
					if (edge.d0.biome != edge.d1.biome) minLength = 3;
					if (edge.d0.ocean && edge.d1.ocean) minLength = 100;
					if (edge.d0.coast || edge.d1.coast) minLength = 1;
					if (edge.river > 0) minLength = 1;
					if (lava.lava.ContainsKey(edge.index) && lava.lava[edge.index])  minLength = 1;
					
					path0 [edge.index] = buildNoisyLineSegments (random, edge.v0.point, t, edge.midpoint, q, minLength);
					path1 [edge.index] = buildNoisyLineSegments (random, edge.v1.point, s, edge.midpoint, r, minLength);
				}
			}
		}
	}

	public static List<Vector2> buildNoisyLineSegments(System.Random random, Vector2 A, Vector2 B, Vector2 C, Vector2 D, int minLength){
		points = new List<Vector2> ();

		points.Add (A);
		subdivide (A, B, C, D, minLength, random);
		points.Add (C);
		return points;
	}

	public static void subdivide(Vector2 A, Vector2 B, Vector2 C, Vector2 D, int minLength, System.Random random){

		if ((A - C).magnitude < minLength || (B - D).magnitude < minLength) {
			return;
		}
		
		var p = (float)random.NextDouble (0.2, 0.8);
		var q = (float)random.NextDouble (0.2, 0.8);
		//var p = Random.Range(0.2F, 0.8F);
		//var q = Random.Range (0.2F, 0.8F);
		
		var E = Vector2.Lerp (A, D, p);
		var F = Vector2.Lerp (B, C, p);
		var G = Vector2.Lerp (A, B, q);
		var I = Vector2.Lerp (D, C, q);
		
		var H = Vector2.Lerp (E, F, q);
		
		var s = 1.0F - (float)random.NextDouble (-1.0, +1.0);
		var t = 1.0F - (float)random.NextDouble (-1.0, +1.0);
		//var s = 1.0F - Random.Range (-0.9F, +0.9F);
		//var t = 1.0F - Random.Range (-0.9F, +0.9F);

		subdivide (A, Vector2.Lerp (G, B, s), H, Vector2.Lerp (E, D, t), minLength, random);
		points.Add (H);
		subdivide (H, Vector2.Lerp (F, C, s), H, Vector2.Lerp (I, D, t), minLength, random);
	}
}
