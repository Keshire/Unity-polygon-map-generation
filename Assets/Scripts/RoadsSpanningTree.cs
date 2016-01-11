using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using graphs;

public class RoadsSpanningTree
{
	public Dictionary<int, int> road; // edge index -> boolean
	public Dictionary<int, List<Edge>> roadConnections;  // center index -> array of Edges with roads

	public RoadsSpanningTree() {
		road = new Dictionary<int, int>();
		roadConnections = new Dictionary<int, List<Edge>>();
	}

	public void createRoads(Map map) {
		var status = new Dictionary<int, string>();  // index -> status (undefined for unvisited, 'fringe', or 'closed')
		//var fringe = new List<Center>();  // locations that are still being analyzed
		//var p:Center, q:Corner, r:Center, edge:Edge, i:int;
		
		// Initialize
		foreach (var edge in map.edges) {
			road[edge.index] = 0;
		}
		
		// Start with the highest elevation Center -- everything else
		// will connect to this location
		var r = map.centers[0];
		foreach (var p in map.centers) {
			if (p.elevation > r.elevation) {
				r = p;
			}
		}

		status[r.index] = "fringe";
		var fringe = new List<Center>(){r};
		
		while (fringe.Count > 0) {
			// Pick a node randomly. Also interesting is to always pick the first or last node.
			int i = Mathf.FloorToInt(Random.value * (float)fringe.Count);
			// i = 0;
			// i = fringe.length - 1;
			if (i > 0 && Random.value < 0.5) i -= 1;
			var p = fringe[i];
			fringe[i] = fringe[0];
			fringe.RemoveAt(0);
			status[p.index] = "closed";
			
			foreach (var edge in p.borders) {
				r = (edge.d0 == p)? edge.d1 : edge.d0;
				if (r != null && !r.water) {
					if (!status.ContainsKey(r.index)) {
						// We've never been here, so let's add this to the fringe
						status[r.index] = "fringe";
						fringe.Add(r);
						road[edge.index] = 1;
					} 
					else if (status[r.index] == "fringe") {
						// We've been here -- what if the cost is lower?  TODO: ignore for now
					}
				}
			}
		}
		
		// Build the roadConnections list from roads
		foreach (var edge in map.edges) {
			if (road[edge.index]>0) {
				Center[] edgeArray = new Center[]{edge.d0, edge.d1};
				foreach (var p in edgeArray) {
					if (p != null) {
						if (!roadConnections.ContainsKey(p.index)) {
							roadConnections[p.index] = new List<Edge>();
						}
						roadConnections[p.index].Add(edge);
					}
				}
			}
		}
		// Rebuild roads from roadConnections
		foreach (var edge in map.edges) {
			if (road[edge.index]>0) {
				Center[] edgeArray = new Center[]{edge.d0, edge.d1};
				foreach (var p in edgeArray) {
					if (p != null) {
						road[edge.index] = Mathf.Max(road[edge.index], roadConnections[p.index].Count);
					}
				}
			}
			road[edge.index] = Mathf.Min(3, Mathf.Max(0, road[edge.index] - 2));
		}
	}


}

