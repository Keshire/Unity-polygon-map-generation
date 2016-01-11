using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

using graphs;
using LibNoise.Unity;
using LibNoise.Unity.Generator;
using LibNoise.Unity.Operator;

public class Map {

	public static float LAKE_THRESHOLD = 0.2F;  // 0 to 1, fraction of water corners for water polygon

	public static float SIZE;

    //public System.Random mapRandom = new System.Random();
    //public UnityEngine.Random mapRandom;
    public float mapRandom;

	// These store the graph data
	public List<Vector2> points; // Only useful during map construction
	public List<Center> centers;
	public List<Corner> corners;
	public List<Edge>   edges;

    //Island Shape
    public object islandShape;
	
	//Random Point distribution
	public PointSelector pointSelector;
	public int numPoints;


	public Map(float size){
		SIZE = size;
		numPoints = 1;
		reset();
	}

	// Random parameters governing the overall shape of the island -- not using variant
	public void newIsland(string islandType, int numPoints_, float seed, float variant){

        if (islandType == "Perlin") { islandShape = new MakePerlin(seed); }
        else if (islandType == "Radial") { islandShape = new MakeRadial(seed); }
        else if (islandType == "Square") { islandShape = new MakeSquare(); }
        else if (islandType == "Hex") { }

		pointSelector = new PointSelector ((int)SIZE, seed);

		numPoints = numPoints_;
        //mapRandom = new System.Random (variant);
        mapRandom = seed;
    }

	public void reset(){
		if (points != null)
			points.Clear();

		if (edges != null) {
			foreach (var edge in edges) {
				edge.d0 = edge.d1 = null;
				edge.v0 = edge.v1 = null;
			}
			edges.Clear();
		}

		if (centers != null) {
			foreach(var p in centers){
				p.neighbors.Clear();
				p.corners.Clear();
				p.borders.Clear();
			}
			centers.Clear();
		}

		if (corners != null) {
			foreach (var q in corners){
				q.adjacent.Clear();
				q.touches.Clear();
				q.protrudes.Clear();
				q.downslope = null;
				q.watershed = null;
			}
			corners.Clear();
		}

		if (points == null) points = new List<Vector2> ();
		if (edges == null) edges = new List<Edge> ();
		if (centers == null) centers = new List<Center> ();
		if (corners == null) corners = new List<Corner> ();
	}
		
	public void go(string pointType){

		/* Place points... */
		reset();

        if (pointType == "Relaxed") points = pointSelector.generateRelaxed(numPoints);
        if (pointType == "Square") points = pointSelector.generateSquare(numPoints);
        if (pointType == "Hex") points = pointSelector.generateHex(numPoints);

        /* Build Graph... */
        var voronoi = new csDelaunay.Voronoi(points,new Rectf(0,0,SIZE,SIZE));
		buildGraph(points, voronoi);
		voronoi.Dispose();
		voronoi = null;
		points = null;
		improveCorners();

        recalcGraph(true);


    }

    public void recalcGraph(bool recalcRivers)
    {
        /* Assign Elevations... */
        assignCornerElevations();
        assignOceanCoastAndLand();
        redistributeElevations(landCorners(corners));
        assignPolygonElevations();


        /* Assign moisture... */
        calculateDownslopes();
        calculateWatersheds();
        if(recalcRivers) createRivers();
        assignCornerMoisture();
        redistributeMoisture(landCorners(corners));
        assignPolygonMoisture();


        /* Decorate map... */
        assignBiomes();
    }

	
	// Although Lloyd relaxation improves the uniformity of polygon
    // sizes, it doesn't help with the edge lengths. Short edges can
    // be bad for some games, and lead to weird artifacts on
    // rivers. We can easily lengthen short edges by moving the
    // corners, but **we lose the Voronoi property**.  The corners are
    // moved to the average of the polygon centers around them. Short
    // edges become longer. Long edges tend to become shorter. The
    // polygons tend to be more uniform after this step.
	public void improveCorners() {
		Vector2[] newCorners = new Vector2[corners.Count];
		Vector2 point;
		
		// First we compute the average of the centers next to each corner.
		foreach (var q in corners) {
			if (q.border) {
				newCorners[q.index] = q.point;
			} 
			else {
				point = Vector2.zero;
				foreach (var r in q.touches) {
					point.x += r.point.x;
					point.y += r.point.y;
				}
				point.x /= q.touches.Count;
				point.y /= q.touches.Count;
				newCorners[q.index] = point;
			}
		}
		
		// Move the corners to the new locations.
		for (var i = 0; i < corners.Count; i++) {
			corners[i].point = newCorners[i];
		}
		
		// The edge midpoints were computed for the old corners and need
		// to be recomputed.
		foreach (var edge in edges) {
			if (edge.v0 !=null && edge.v1 !=null) {
				edge.midpoint = Vector2.Lerp(edge.v0.point, edge.v1.point, 0.5F);
			}
		}
	}

	
	// Create an array of corners that are on land only, for use by
    // algorithms that work only on land.  We return an array instead
    // of a vector because the redistribution algorithms want to sort
    // this array using Array.sortOn.
	List<Corner> landCorners(List<Corner> corners){
		List<Corner> locations = new List<Corner>();
		foreach (var q in corners) {
			if(!q.ocean && !q.coast){
				locations.Add(q);
			}
		}
		return locations;
	}
	
	
	// Build graph data structure in 'edges', 'centers', 'corners',
    // based on information in the Voronoi results: point.neighbors
    // will be a list of neighboring points of the same type (corner
    // or center); point.edges will be a list of edges that include
    // that point. Each edge connects to four points: the Voronoi edge
    // edge.{v0,v1} and its dual Delaunay triangle edge edge.{d0,d1}.
    // For boundary polygons, the Delaunay edge will have one null
    // point, and the Voronoi edge may be null.
	public void buildGraph(List<Vector2> points, csDelaunay.Voronoi voronoi){

		Center p;
		var centerLookup = new Dictionary<Vector2, Center> ();
		var libedges = voronoi.Edges;


		// Build Center objects for each of the points, and a lookup map
		// to find those Center objects again as we build the graph
		foreach (var point in points) {
			p = new Center();
			p.index = centers.Count;
			p.point = point;
			p.neighbors = new List<Center>();
			p.borders =   new List<Edge>();
			p.corners =   new List<Corner>();
			centers.Add(p);
			centerLookup[point] = p;
		}

		// Workaround for Voronoi lib bug: we need to call region() before Edges or neighboringSites are available
		foreach (var c in centers) {
			voronoi.Region(c.point);		
		}


		// The Voronoi library generates multiple Point objects for
		// corners, and we need to canonicalize to one Corner object.
		// To make lookup fast, we keep an array of Points, bucketed by
		// x value, and then we only have to look at other Points in
		// nearby buckets. When we fail to find one, we'll create a new
		// Corner object.
		var  _cornerMap = new Dictionary<int, List<Corner>> ();
		Func<Vector2, Corner> makeCorner = delegate(Vector2 point) {
			int bucket;
			
			if (point == Vector2.zero) { return null; }
			
			for (bucket = (int)(point.x)-1; bucket <= (int)(point.x)+1; bucket++) {
				if(_cornerMap.ContainsKey(bucket)){
					foreach(var c in _cornerMap[bucket]){
						var dx = point.x - c.point.x;
						var dy = point.y - c.point.y;
						if(dx*dx+dy*dy<1e-6F){
							return c;
						}
					}
				}
			}
			
			bucket = (int)point.x;
			if (!_cornerMap.ContainsKey (bucket)) {
				_cornerMap.Add(bucket, null);
			}
			if(_cornerMap[bucket] == null) {_cornerMap[bucket] = new List<Corner>();}
			
			
			var q = new Corner ();
			q.index = corners.Count;
			corners.Add (q);
			q.point = point;
			q.border = (point.x == 0F || point.x == SIZE || point.y == 0F || point.y == SIZE);
			q.touches = new List<Center> ();
			q.protrudes = new List<Edge> ();
			q.adjacent = new List<Corner> ();
			_cornerMap[bucket].Add(q);
			
			return q;
		};

		// Helper functions for the following for loop;
		Action<List<Corner>, Corner> addToCornerList = delegate(List<Corner> v, Corner x) 
        {
			if (x != null && v.IndexOf (x) < 0) { v.Add (x); }
		};
		Action<List<Center>, Center> addToCenterList = delegate(List<Center> v, Center x) 
        {
			if (x != null && v.IndexOf (x) < 0) { v.Add (x); }
		};

		foreach (var libedge in libedges) {
			var dedge = libedge.delaunayLine();
			var vedge = libedge.voronoiEdge();

			var edge = new Edge();
			edge.index = edges.Count;
			edge.river = 0;
			edges.Add(edge);
			if (vedge.p0 != Vector2.zero && vedge.p1 != Vector2.zero)
            {
					edge.midpoint = Vector2.Lerp(vedge.p0, vedge.p1, 0.5F);
			}

			
			edge.v0 = makeCorner(vedge.p0);
			edge.v1 = makeCorner(vedge.p1);
			edge.d0 = centerLookup[dedge.p0];
			edge.d1 = centerLookup[dedge.p1];
			
			if (edge.d0 != null) { edge.d0.borders.Add(edge); }
			if (edge.d1 != null) { edge.d1.borders.Add(edge); }
			if (edge.v0 != null) { edge.v0.protrudes.Add(edge); }
			if (edge.v1 != null) { edge.v1.protrudes.Add(edge); }
			
			if (edge.d0 != null && edge.d1 != null) {
				addToCenterList(edge.d0.neighbors, edge.d1);
				addToCenterList(edge.d1.neighbors, edge.d0);
			}
			if (edge.v0 != null && edge.v1 != null) {
				addToCornerList(edge.v0.adjacent, edge.v1);
				addToCornerList(edge.v1.adjacent, edge.v0);
			}
			if (edge.d0 != null) {
				addToCornerList(edge.d0.corners, edge.v0);
				addToCornerList(edge.d0.corners, edge.v1);
			}
			if (edge.d1 != null) {
				addToCornerList(edge.d1.corners, edge.v0);
				addToCornerList(edge.d1.corners, edge.v1);
			}
			if (edge.v0 != null) {
				addToCenterList(edge.v0.touches, edge.d0);
				addToCenterList(edge.v0.touches, edge.d1);
			}
			if (edge.v1 != null) {
				addToCenterList(edge.v1.touches, edge.d0);
				addToCenterList(edge.v1.touches, edge.d1);
			}
		}
	}


	// Determine elevations and water at Voronoi corners. By
    // construction, we have no local minima. This is important for
    // the downslope vectors later, which are used in the river
    // construction algorithm. Also by construction, inlets/bays
    // push low elevation areas inland, which means many rivers end
    // up flowing out through them. Also by construction, lakes
    // often end up on river paths because they don't raise the
    // elevation as much as other terrain does.
	public void assignCornerElevations() {
		Queue<Corner> queue = new Queue<Corner> ();
		foreach (var q in corners) {
			q.water = !inside(q.point);
		}
		foreach (var q in corners) {
			if (q.border) {
				q.elevation = 0.0F;
				queue.Enqueue(q);
			} 
			else {
				q.elevation = float.PositiveInfinity;
			}
		}
		while (queue.Count > 0) {
			Corner q = queue.Dequeue();
			
			foreach (var s in q.adjacent) {
				var newElevation = 0.01F + q.elevation;
				if (!q.water && !s.water) {
					newElevation += 1F;
				}
				if (newElevation < s.elevation) {
					s.elevation = newElevation;
					queue.Enqueue(s);
				}
			}
		}
	}

	
	// Change the overall distribution of elevations so that lower
    // elevations are more common than higher
    // elevations. Specifically, we want elevation X to have frequency
    // (1-X).  To do this we will sort the corners, then set each
    // corner to its desired elevation.
	public void redistributeElevations(List<Corner> locations) {
		// SCALE_FACTOR increases the mountain area. At 1.0 the maximum
		// elevation barely shows up on the map, so we set it to 1.1.
		var SCALE_FACTOR = 1.1F;
		float x, y;
		
		locations.Sort(delegate(Corner e1, Corner e2){ return e1.elevation.CompareTo(e2.elevation); });
		
		for (var i = 0; i < locations.Count; i++)
        {
			y = (float)i/((float)locations.Count-1F);
			x = Mathf.Sqrt(SCALE_FACTOR) - Mathf.Sqrt(SCALE_FACTOR*(1F-y));

			if (x > 1.0F) x = 1.0F;  // TODO: does this break downslopes?

			locations[i].elevation = x;
		}
	}

	
	// Change the overall distribution of moisture to be evenly distributed.
	public void redistributeMoisture(List<Corner> locations) {

		locations.Sort(delegate(Corner m1, Corner m2){ return m1.moisture.CompareTo(m2.moisture);});
		
		for (int i = 0; i < locations.Count; i++) {
			locations[i].moisture = (float)(i/(locations.Count-1F));
		}
	}
	
	
	// Determine polygon and corner types: ocean, coast, land.
	public void assignOceanCoastAndLand() {
		Queue<Center> queue = new Queue<Center>();

		int numWater;
		int numOcean;
		int numLand;
		
		foreach (var p in centers) {
			numWater = 0;
			foreach (var q in p.corners) {
				if (q.border) {
					p.border = true;
					p.ocean = true;
					q.water = true;
					queue.Enqueue(p);
				}
				if (q.water) {
					numWater += 1;
				}
			}
			p.water = (p.ocean || numWater >= p.corners.Count * LAKE_THRESHOLD);
		}
		while (queue.Count > 0) {
			var p = queue.Dequeue();
			foreach (var r in p.neighbors) {
				if (r.water && !r.ocean) {
					r.ocean = true;
					queue.Enqueue(r);
				}
			}
		}
		foreach (var p in centers) {
			numOcean = 0;
			numLand = 0;
			foreach (var r in p.neighbors) {
				numOcean += r.ocean?1:0;
				numLand += !r.water?1:0;
			}
			p.coast = (numOcean > 0) && (numLand > 0);
		}
		foreach (var q in corners) {
			numOcean = 0;
			numLand = 0;
			foreach (var p in q.touches) {
				numOcean += p.ocean?1:0;
				numLand += !p.water?1:0;
			}
			q.ocean = (numOcean == q.touches.Count);
			q.coast = (numOcean > 0) && (numLand > 0);
			q.water = q.border || ((numLand != q.touches.Count) && !q.coast);
		}
	}
	

	// Polygon elevations are the average of the elevations of their corners.
	public void assignPolygonElevations() {
		float sumElevation;

        foreach (var q in corners)
        {
            if (q.ocean || q.coast)
            {
                q.elevation = 0.0F;
            }
        }


        foreach (var p in centers) {
			sumElevation = 0.0F;
			foreach (var q in p.corners) {
				sumElevation += q.elevation;
			}
			p.elevation = sumElevation / p.corners.Count;
		}
	}

	
	// Calculate downslope pointers.  At every point, we point to the
    // point downstream from it, or to itself.  This is used for
    // generating rivers and watersheds.
	public void calculateDownslopes() {
		Corner r;
		foreach (var q in corners) {
			r = q;
			foreach (var s in q.adjacent) {
				if (s.elevation <= r.elevation) {
					r = s;
				}
			}
			q.downslope = r;
		}
	}

	
	// Calculate the watershed of every land point. The watershed is
    // the last downstream land point in the downslope graph. TODO:
    // watersheds are currently calculated on corners, but it'd be
    // more useful to compute them on polygon centers so that every
    // polygon can be marked as being in one watershed.
	public void calculateWatersheds() {
		//var q:Corner, r:Corner, i:int,
		Corner r;
		bool changed;
		
		// Initially the watershed pointer points downslope one step.      
		foreach (var q in corners) {
			q.watershed = q;
			if (!q.ocean && !q.coast) {
				q.watershed = q.downslope;
			}
		}
		for (var i = 0; i < 100; i++) {
			changed = false;
			foreach (var q in corners) {
				if (!q.ocean && !q.coast && !q.watershed.coast) {
					r = q.downslope.watershed;
					if (!r.ocean) q.watershed = r;
					changed = true;
				}
			}
			if (!changed) break;
		}
		foreach (var q in corners) {
			r = q.watershed;
			r.watershed_size = 1 + r.watershed_size;
		}
	}

	
	// Create rivers along edges. Pick a random corner point, then
    // move downslope. Mark the edges and corners as rivers.
	public void createRivers() {
		Corner q;
		Edge edge;
		

		for (var i = 0; i < SIZE/2; i++) {
			q = corners[(int)UnityEngine.Random.Range(0f, (float)corners.Count-1f)];
			if (q.ocean || q.elevation < 0.3F || q.elevation > 0.9F) continue;
			while (!q.coast) {
				if (q == q.downslope) {
					break;
				}
				edge = lookupEdgeFromCorner(q, q.downslope);
				edge.river += 1;
				q.river += 1;
				q.downslope.river += 1;  // TODO: fix double count
				q = q.downslope;
			}
		}
	}

	
	// Calculate moisture. Freshwater sources spread moisture: rivers
    // and lakes (not oceans). Saltwater sources have moisture but do
    // not spread it (we set it at the end, after propagation).
	public void assignCornerMoisture() {
		//Corner q; 
		float newMoisture;
		Queue<Corner> queue = new Queue<Corner>();
		
		// Fresh water
		foreach (var q in corners) {
			if ((q.water || q.river > 0) && !q.ocean) {
				q.moisture = q.river > 0? Mathf.Min(3.0F, (0.2F * q.river)) : 1.0F;
				queue.Enqueue(q);
			} else {
				q.moisture = 0.0F;
			}
		}
		while (queue.Count > 0) {
			var q = queue.Dequeue();
			
			foreach (var r in q.adjacent) {
				newMoisture = q.moisture * 0.9F;
				if (newMoisture > r.moisture) {
					r.moisture = newMoisture;
					queue.Enqueue(r);
				}
			}
		}
		// Salt water
		foreach (var q in corners) {
			if (q.ocean || q.coast) {
				q.moisture = 1.0F;
			}
		}
	}
	
	
	// Polygon moisture is the average of the moisture at corners
	public void assignPolygonMoisture() {
		float sumMoisture;
		foreach (var p in centers) {
			sumMoisture = 0.0F;
			foreach (var q in p.corners) {
				if (q.moisture > 1.0F) q.moisture = 1.0F;

				sumMoisture += q.moisture;
			}
			p.moisture = sumMoisture / p.corners.Count;
		}
	}
	
	
	// Assign a biome type to each polygon. If it has
    // ocean/coast/water, then that's the biome; otherwise it depends
    // on low/high elevation and low/medium/high moisture. This is
    // roughly based on the Whittaker diagram but adapted to fit the
    // needs of the island map generator.
	static public string getBiome(Center p) {
		if (p.ocean) {
			return "OCEAN";
		} else if (p.water) {
			if (p.elevation < 0.1F) return "MARSH";
			if (p.elevation > 0.8F) return "ICE";
			return "LAKE";
		} else if (p.coast) {
			return "BEACH";
		} else if (p.elevation > 0.8F) {
			if (p.moisture > 0.50F) return "SNOW";
			else if (p.moisture > 0.33F) return "TUNDRA";
			else if (p.moisture > 0.16F) return "BARE";
			else return "SCORCHED";
		} else if (p.elevation > 0.6F) {
			if (p.moisture > 0.66F) return "TAIGA";
			else if (p.moisture > 0.33F) return "SHRUBLAND";
			else return "TEMPERATE_DESERT";
		} else if (p.elevation > 0.3F) {
			if (p.moisture > 0.83F) return "TEMPERATE_RAIN_FOREST";
			else if (p.moisture > 0.50F) return "TEMPERATE_DECIDUOUS_FOREST";
			else if (p.moisture > 0.16F) return "GRASSLAND";
			else return "TEMPERATE_DESERT";
		} else {
			if (p.moisture > 0.66F) return "TROPICAL_RAIN_FOREST";
			else if (p.moisture > 0.33F) return "TROPICAL_SEASONAL_FOREST";
			else if (p.moisture > 0.16F) return "GRASSLAND";
			else return "SUBTROPICAL_DESERT";
		}
	}
	
	public void assignBiomes() {
		foreach (var p in centers) {
			p.biome = getBiome(p);
		}
	}
	
	
	// Look up a Voronoi Edge object given two adjacent Voronoi
    // polygons, or two adjacent Voronoi corners
	public Edge lookupEdgeFromCenter(Center p, Center r) {
		foreach (var edge in p.borders) {
			if (edge.d0 == r || edge.d1 == r) return edge;
		}
		return null;
	}
	
	public Edge lookupEdgeFromCorner(Corner q, Corner s) {
		foreach (var edge in q.protrudes) {
			if (edge.v0 == s || edge.v1 == s) return edge;
		}
		return null;
	}

	// Determine whether a given point should be on the island or in the water.
	public bool inside(Vector2 p) {

        if (islandShape.GetType() == typeof(MakePerlin))
        {
            var islandReturn = (MakePerlin)islandShape;
            return islandReturn.returnPerlin(new Vector2(2F * (p.x / SIZE - 0.5F), 2F * (p.y / SIZE - 0.5F)));
        }
        else if (islandShape.GetType() == typeof(MakeRadial))
        {
            var islandReturn = (MakeRadial)islandShape;
            return islandReturn.returnRadial(new Vector2(2F * (p.x / SIZE - 0.5F), 2F * (p.y / SIZE - 0.5F)));
        }
        else if (islandShape.GetType() == typeof(MakeSquare))
        {
            var islandReturn = (MakeSquare)islandShape;
            return islandReturn.returnSquare(new Vector2(2F * (p.x / SIZE - 0.5F), 2F * (p.y / SIZE - 0.5F)));
        }
        else { return false; }
    }
}


public class MakeSquare {
	public bool returnSquare(Vector2 q){ return true; }
}

public class MakePerlin {
	
	public Noise2D noiseMap = null;
	private double m_frequency = 2.0;
	private double m_lacunarity = 2.0;
	private QualityMode m_quality = QualityMode.Low;
	private int m_octaveCount = 8;
	private double m_persistence = 0.60;

	public Texture2D perlin;
	
	public MakePerlin(float seed) {	

		perlin = new Texture2D (256, 256);
		ModuleBase moduleBase;
		moduleBase = new Perlin(m_frequency, m_lacunarity, m_persistence, m_octaveCount, (int)seed, m_quality);
		noiseMap = new Noise2D(perlin.width, perlin.height, moduleBase);

		float zoom = 1f; 
		float offset = 0f;
		noiseMap.GeneratePlanar(
			offset + -1 * 1/zoom, 
			offset + offset + 1 * 1/zoom, 
			offset + -1 * 1/zoom,
			offset + 1 * 1/zoom);

		perlin = noiseMap.GetTexture ();
	}
	
	public bool returnPerlin(Vector2 q){
		float c = perlin.GetPixel ((int)((q.x + 1) * (perlin.height/2)), (int)((q.y + 1) * (perlin.width/2))).grayscale;
		return c > (0.3 + 0.3 * q.magnitude * q.magnitude);
	}

}

public class MakeRadial {
	public static float ISLAND_FACTOR = 1.07f;

	public static System.Random islandRandom = new System.Random();
	public static int bumps = islandRandom.Next(1, 6);
	public static double startAngle = islandRandom.NextDouble(0, 2*System.Math.PI);
	public static double dipAngle = islandRandom.NextDouble(0, 2*System.Math.PI);
	public static double dipWidth = islandRandom.NextDouble(0.2, 0.7);

	public MakeRadial(float seed) {

		//islandRandom = new System.Random(seed);
		bumps = islandRandom.Next(1, 6);
		startAngle = islandRandom.NextDouble(0, 2*System.Math.PI);
		dipAngle = islandRandom.NextDouble(0, 2*System.Math.PI);
		dipWidth = islandRandom.NextDouble(0.2, 0.7);
	}

	public bool returnRadial(Vector2 q){
		var angle = System.Math.Atan2 (q.y, q.x);
		var length = 0.5 * (System.Math.Max (System.Math.Abs (q.x), System.Math.Abs (q.y)) + q.magnitude);

		var r1 = 0.5 + 0.40*System.Math.Sin(startAngle + bumps*angle + System.Math.Cos((bumps+3)*angle));
		var r2 = 0.7 - 0.20*System.Math.Sin(startAngle + bumps*angle - System.Math.Sin((bumps+2)*angle));
		if (System.Math.Abs(angle - dipAngle) < dipWidth
		    || System.Math.Abs(angle - dipAngle + 2*System.Math.PI) < dipWidth
		    || System.Math.Abs(angle - dipAngle - 2*System.Math.PI) < dipWidth) {
			r1 = r2 = 0.2;
		}
		return  (length < r1 || (length > r1*ISLAND_FACTOR && length < r2));
	}
}

public class PointSelector {
	
	public int size;
	public float seed;

	private const int LOYDRELAXATIONS = 2;

	public PointSelector(int SIZE, float SEED){
		size = SIZE;
		seed = SEED;
	}

	public List<Vector2> generateSquare(int numPoints){
		List<Vector2> points = new List<Vector2>();
		var N = System.Math.Sqrt (numPoints);
		for (var x = 0; x < N; x++) {
			for (var y = 0; y < N; y++) {
				points.Add(new Vector2((0.5F + (float)x)/(float)N * (float)size, (0.5F + (float)y)/(float)N * (float)size));
			}	
		}
		return points;
	}

    public List<Vector2> generateHex(int numPoints)
    {
        List<Vector2> points = new List<Vector2>();
        var N = System.Math.Sqrt(numPoints);
        for (var x = 0; x < N; x++)
        {
            for (var y = 0; y < N; y++)
            {
                points.Add(new Vector2((0.5F + (float)x) / (float)N * (float)size, (0.25F + 0.5F*(float)x % 2 + (float)y) / (float)N * (float)size));
            }
        }
        return points;
    }

    public List<Vector2> generateRelaxed(int numPoints){

		List<Vector2> points = new List<Vector2>();
		//var mapRandom = new System.Random (seed);

		for (var i = 0; i < numPoints; i++) {
			points.Add(new Vector2(UnityEngine.Random.Range(10F, size-10F), UnityEngine.Random.Range(10F, size-10F)));
		}
		for (var i = 0; i < LOYDRELAXATIONS; i++) {
			var voronoi = new csDelaunay.Voronoi(points,new Rectf(0,0,size,size));
			for (var j = 0; j < points.Count; j++){
				Vector2 p = points[j];
				var region = voronoi.Region(p);
				float x = 0F;
				float y = 0F;
				foreach (Vector2 c in region){
					x += c.x;
					y += c.y;
				}
				x /= region.Count;
				y /= region.Count;
				p.x = x;
				p.y = y;
				points[j] = p;
			}
		}

		return points;
	}
}