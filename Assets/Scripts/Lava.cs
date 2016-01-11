using UnityEngine;
using System.Collections;
using System.Collections.Generic;

using graphs;

public class Lava {
	static public float FRACTION_LAVA_FISSURES = 0.05F;  // 0 to 1, probability of fissure
	static public int FRACTION_LAVA_TUBES = 2; // Number of edges needed before biome flips to lava
		
	// The lava array marks the edges that hava lava.
	public Dictionary<int, bool> lava = new Dictionary<int, bool> ();
		
	// Lava fissures are at high elevations where moisture is low
	public void createLava(Map map/*, System.Random mapRandom*/) {
		foreach (var edge in map.edges) {
			if (edge.river == 0 && !edge.d0.water && !edge.d1.water
				   && edge.d0.elevation > 0.8F && edge.d1.elevation > 0.8F
				   && edge.d0.moisture < 0.3F && edge.d1.moisture < 0.3F
				   && Random.value < FRACTION_LAVA_FISSURES) {
				lava[edge.index] = true;
				//Debug.Log(lava[edge.index]);
			}
		}
		foreach (var p in map.centers) {
			int numLavaEdges = 0;
			foreach (var edge in p.borders){
				if (lava.ContainsKey(edge.index) && lava[edge.index] == true) numLavaEdges += 1;
			}
			//How many sides before the while poly is LAVA
			if (numLavaEdges > FRACTION_LAVA_TUBES) p.biome = "LAVA";
		}
	}
}