using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace graphs {

	public class Center {
		public int index;
		public Vector2 point;
		public bool water;
		public bool ocean;
		public bool coast;
		public bool border;
		public string biome;
		public float elevation;
		public float moisture;
		public List<Center> neighbors;
		public List<Edge> borders;
		public List<Corner> corners;
	}
	public class Corner {
		public int index;
		public Vector2 point;
		public bool water;
		public bool ocean;
		public bool coast;
		public bool border;
		public string biome;
		public float elevation;
		public float moisture;
		public int river;
		public int watershed_size;
		public Corner downslope;
		public Corner watershed;
		public List<Center> touches;
		public List<Edge> protrudes;
		public List<Corner> adjacent;
	}
	public class Edge {
		public int index;
		public int river;
		public Center d0, d1;
		public Corner v0, v1;
		public Vector2 midpoint;
	}
}