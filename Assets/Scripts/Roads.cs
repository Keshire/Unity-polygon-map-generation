using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using graphs;

public class Roads {
	// The road array marks the edges that are roads.  The mark is 1,
	// 2, or 3, corresponding to the three contour levels. Note that
	// these are sparse arrays, only filled in where there are roads.
	public Dictionary<int, int> road;  // edge index -> int contour level
	public Dictionary<int, List<Edge>> roadConnections;  // center index -> array of Edges with roads
	
	public Roads() {
		road = new Dictionary<int, int>();
		roadConnections = new Dictionary<int, List<Edge>>();
	}
	
	
	// We want to mark different elevation zones so that we can draw
	// island-circling roads that divide the areas.
	public void createRoads(Map map) {
		// Oceans and coastal polygons are the lowest contour zone
		// (1). Anything connected to contour level K, if it's below
		// elevation threshold K, or if it's water, gets contour level
		// K.  (2) Anything not assigned a contour level, and connected
		// to contour level K, gets contour level K+1.
		var queue = new Queue<Center> ();
		var elevationThresholds = new float[]{0F, 0.05F, 0.37F, 0.64F};

		var cornerContour = new Dictionary<int, int>();  // corner index -> int contour level
		var centerContour = new Dictionary<int, int>();  // center index -> int contour level
		
		foreach (Center p in map.centers) {
			if (p.coast || p.ocean) {
				centerContour[p.index] = 1;
				queue.Enqueue(p);
			}
		}
		
		while (queue.Count > 0) {
			var p = queue.Dequeue();
			foreach (var r in p.neighbors) {
				var newLevel = centerContour.ContainsKey(p.index)?centerContour[p.index]:0;

				while (newLevel < elevationThresholds.Length && (r.elevation > elevationThresholds[newLevel] && !r.water)) {
					// NOTE: extend the contour line past bodies of water so that roads don't terminate inside lakes.
					newLevel += 1;
				}

				if (centerContour.ContainsKey(r.index)){
					if (newLevel < centerContour[r.index]){
						centerContour[r.index] = newLevel;
						queue.Enqueue(r);
					}
				}
			}
		}
		
		// A corner's contour level is the MIN of its polygons
		foreach (Center p in map.centers) {
			foreach (var q in p.corners) {
				int c1 = cornerContour.ContainsKey(q.index)?cornerContour[q.index]:999;
				int c2 = centerContour.ContainsKey(p.index)?centerContour[p.index]:999;
				cornerContour[q.index] = Mathf.Min(c1,c2);
			}
		}
		
		// Roads go between polygons that have different contour levels
		foreach (Center p in map.centers) {
			foreach (var edge in p.borders) {
				if (edge.v0 != null && edge.v1 !=null && cornerContour[edge.v0.index] != cornerContour[edge.v1.index]) {
					road[edge.index] = Mathf.Min(cornerContour[edge.v0.index], cornerContour[edge.v1.index]);
					if (!roadConnections.ContainsKey(p.index)) {
						roadConnections[p.index] = new List<Edge>();
					}
					roadConnections[p.index].Add(edge);
				}
			}
		}
	}
	
}