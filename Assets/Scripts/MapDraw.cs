using UnityEngine;
using Vectrosity; 
using System.Collections;
using System.Collections.Generic;

using graphs;

public class MapDraw : MonoBehaviour {

	public string islandType = "Perlin";
	public string pointType = "Relaxed";
	public static string mapMode = "polygon";
	public int numPoints = 200;
	public float seed = 0;
	public float variant = 0;

	public static int SIZE;
	public static int islandSeedInitial = 1;
	public static UnityEngine.Random random;
	
	public Map map;
	public Roads roads;
	//public RoadsSpanningTree roads;
	public Lava lava;
	//public Watersheds watersheds;
	public NoisyEdges noisyEdges;
	public Material mat;

    public Mesh mesh;

    public List<VectorLine> riverarray;

    void Start () {

		SIZE = Screen.height;
        //random = new System.Random (islandSeedInitial);
        Random.seed = islandSeedInitial;

        map = new Map (SIZE);
        go(islandType, pointType, numPoints);

        Camera.main.transform.position = new Vector3(mesh.bounds.center.x, mesh.bounds.center.y, -10f);

    }
	void Update() {
		if (Input.GetKeyDown("space")) {

            //cleanup
            mesh.Clear();
            VectorLine.Destroy(riverarray);
            map = null;

            seed = Random.value;
			variant = Random.value;
            map = new Map (SIZE);
			go(islandType, pointType, numPoints);
		}

        if (Input.GetMouseButtonUp(0))
        {
            map.recalcGraph(false); //Don't calc rivers until we fix infinit expanding (which also messes with moisture calc)
            renderDebugPolygons();
            //drawMap(mapMode);
        }

    }

	/* Start Building map */
	public void go(string newIslandType, string newPointType, int newNumPoints){
		//roads = new Roads();
		//roads = new RoadsSpanningTree ();
		lava = new Lava();
		//watersheds = new Watersheds();
		//noisyEdges = new NoisyEdges();

		newIsland(newIslandType, newNumPoints);

		map.go(newPointType);
        //roads.createRoads(map);
        lava.createLava(map/*, map.mapRandom*/);
        //watersheds.createWatersheds(map);
        //noisyEdges.buildNoisyEdges(map, lava, map.mapRandom);

        /* draw data */
        drawMap(mapMode);
    }

	public void newIsland(string newIslandType, int newNumPoints){
		islandType = newIslandType;
		numPoints = newNumPoints;
		map.newIsland(islandType, numPoints, seed, variant);
	}

	public void drawMap(string mode) {

        renderDebugPolygons();
        renderEdges ();
        
        //renderNoisyPolygons (); //Too many verts!
        //renderNoisyEdges ();

        //renderRoads ();
        //renderBridges ();
    }

    public void renderDebugPolygons()
    {

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshRenderer>().material = mat;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color32> colors = new List<Color32>();
        List<int> tris = new List<int>();

        //var zScale = 0.15f * SIZE;

        int triCount = 0;
        foreach (var p in map.centers)
        {

            if (p.ocean) continue;

            foreach (var edge in p.borders)
            {

                //var color = displayColors[p.biome];
                //var color = colorWithSlope(displayColors[p.biome], p, null, edge);
                var color = colorWithSmoothColors(displayColors[p.biome], p, null, edge);

                Corner corner0 = edge.v0;
                Corner corner1 = edge.v1;

                if (corner0 == null || corner1 == null) continue; //edge of map

                //In case we want elevation
                //var zcenter = zScale * p.elevation;
                //var zcorner0 = zScale * corner0.elevation;
                //var zcorner1 = zScale * corner1.elevation;

                verts.Add(new Vector3(p.point.x, p.point.y, 0f));
                uvs.Add(new Vector2(0f, 0f));
                colors.Add(color);
                tris.Add(triCount);
                triCount++;
                verts.Add(new Vector3(corner0.point.x, corner0.point.y, 0f));
                uvs.Add(new Vector2(1f, 0f));
                colors.Add(color);
                tris.Add(triCount);
                triCount++;
                verts.Add(new Vector3(corner1.point.x, corner1.point.y, 0f));
                uvs.Add(new Vector2(0f, 1f));
                colors.Add(color);
                tris.Add(triCount);
                triCount++;
            }
        }

        mesh.vertices = verts.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors32 = colors.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

    }

    public void renderEdges()
    {
        riverarray = new List<VectorLine>();

        foreach (var p in map.centers)
        {
            foreach (var r in p.neighbors)
            {
                var edge = map.lookupEdgeFromCenter(p, r);
                Color32 colors;
                float width = 1;

                if (p.ocean != r.ocean)
                {
                    // One side is ocean and the other side is land -- coastline
                    width = 1;
                    colors = displayColors["COAST"];
                }
                else if ((p.water) != (r.water) && p.biome != "ICE" && r.biome != "ICE")
                {
                    // Lake boundary
                    width = 1;
                    colors = displayColors["LAKESHORE"];
                }
                else if (p.water || r.water)
                {
                    continue; // Lake interior – we don't want to draw the rivers here
                }
                /* 
                // The fissures looked goofy, so I changed the full poly to lava and we'll ring them with scorch.
                else if (lava.lava.ContainsKey(edge.index) && lava.lava[edge.index]) {
					
					width = 1;
					//GL.Color(displayColors["SCORCHED"]);
				} 
                */
                else if (edge.river > 0)
                {
                    // River edge
                    width = edge.river;
                    colors = displayColors["RIVER"];
                }
                else
                {
                    continue; // No edge
                }
                
                var riverLine = new VectorLine("riverLine", new List<Vector3>() {
                    new Vector3(edge.v0.point.x,edge.v0.point.y,-1f),
                    new Vector3(edge.v1.point.x,edge.v1.point.y,-1f) }, 
                    width);

                riverLine.SetColor(colors);
                riverLine.Draw3D();
                riverarray.Add(riverLine);  
            }
        }  
    }

    /* Rendering function */
    public void renderNoisyPolygons(){

        mesh = new Mesh();
        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshRenderer>().material = mat;

        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<Color32> colors = new List<Color32>();
        List<int> tris = new List<int>();

        int triCount = 0;
        foreach (Center p in map.centers) {

            if (p.ocean) continue;

            foreach (Edge edge in p.borders) {
				Color32 color = colorWithSlope(displayColors[p.biome], p, null, edge);

                Corner corner0 = edge.v0;
                Corner corner1 = edge.v1;

                if (corner0 == null || corner1 == null) continue; //edge of map


                /* I'm not sure what needs to be done here, but this seems to work */
                if (!noisyEdges.path0.ContainsKey(edge.index) || !noisyEdges.path1.ContainsKey(edge.index)) continue;
				if (noisyEdges.path0[edge.index] == null || noisyEdges.path1[edge.index] == null) continue;

				List<Vector2> path0 = noisyEdges.path0[edge.index];
                List<Vector2> path1 = noisyEdges.path1[edge.index];

				for (var i = 0; i < path0.Count-1; i++) {
                    verts.Add(new Vector3(p.point.x, p.point.y, 0f));
                    uvs.Add(new Vector2(0f, 0f));
                    colors.Add(color);
                    tris.Add(triCount);
                    triCount++;
                    verts.Add(new Vector3(path0[i].x, path0[i].y, 0f));
                    uvs.Add(new Vector2(1f, 0f));
                    colors.Add(color);
                    tris.Add(triCount);
                    triCount++;
                    verts.Add(new Vector3(path0[i + 1].x, path0[i + 1].y, 0f));
                    uvs.Add(new Vector2(0f, 1f));
                    colors.Add(color);
                    tris.Add(triCount);
                    triCount++;
				}
				for (var i = 0; i < path1.Count-1; i++) {
                    verts.Add(new Vector3(p.point.x, p.point.y, 0f));
                    uvs.Add(new Vector2(0f, 0f));
                    colors.Add(color);
                    tris.Add(triCount);
                    triCount++;
                    verts.Add(new Vector3(path1[i].x, path1[i].y, 0f));
                    uvs.Add(new Vector2(1f, 0f));
                    colors.Add(color);
                    tris.Add(triCount);
                    triCount++;
                    verts.Add(new Vector3(path1[i + 1].x, path1[i + 1].y, 0f));
                    uvs.Add(new Vector2(0f, 1f));
                    colors.Add(color);
                    tris.Add(triCount);
                    triCount++;
                }
			}
		}

        mesh.vertices = verts.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.colors32 = colors.ToArray();
        mesh.triangles = tris.ToArray();
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }

    public void renderNoisyEdges()
    {
        GL.Begin(GL.QUADS);
        foreach (var p in map.centers)
        {
            foreach (var r in p.neighbors)
            {
                var edge = map.lookupEdgeFromCenter(p, r);
                float width = 1F;

                if (!noisyEdges.path0.ContainsKey(edge.index) || !noisyEdges.path1.ContainsKey(edge.index))
                {
                    continue;
                }
                if (noisyEdges.path0[edge.index] == null || noisyEdges.path1[edge.index] == null)
                {
                    continue;
                }
                if (p.ocean != r.ocean)
                {
                    // One side is ocean and the other side is land -- coastline
                    width = 0.5f;
                    GL.Color(displayColors["COAST"]);
                }
                else if ((p.water) != (r.water) && p.biome != "ICE" && r.biome != "ICE")
                {
                    // Lake boundary
                    width = 0.5f;
                    GL.Color(displayColors["LAKESHORE"]);
                }
                else if (p.water || r.water)
                {
                    // Lake interior – we don't want to draw the rivers here
                    continue;
                } /* else if (lava.lava.ContainsKey(edge.index) && lava.lava[edge.index]) {
					// Lava flow
					width = 1;
					GL.Color(displayColors["LAVA"]);
				}*/
                else if (edge.river > 0)
                {
                    // River edge
                    width = Mathf.Sqrt(edge.river);
                    GL.Color(displayColors["RIVER"]);
                }
                else
                {
                    // No edge
                    continue;
                }

                var path0 = noisyEdges.path0[edge.index];
                var path1 = noisyEdges.path1[edge.index];

                for (var i = 0; i < path0.Count - 1; i++)
                {
                    drawQuadLine(path0[i], path0[i + 1], width);
                }
                for (var i = path1.Count - 1; i >= 1; i--)
                {
                    drawQuadLine(path1[i], path1[i - 1], width);
                }
            }
        }
        GL.End();
    }

    /* Coloring Function */
    public Color32 colorWithSmoothColors(Color32 color, Center p, Center q, Edge edge){

        
		if (q != null && p.water == q.water)
        {
			color = Color32.Lerp(displayColors[p.biome], displayColors[q.biome], 0.5F);		
		}
        if (q == null && (p.water == edge.d0.water) && (p.water == edge.d1.water))
        {
            var tempColor = Color32.Lerp(displayColors[edge.d0.biome], displayColors[edge.d1.biome], 0.25F);
            color = Color32.Lerp(displayColors[p.biome], tempColor, 0.25F);
        }
        
		return color;

	}
	
	public Color32 colorWithSlope(Color32 color, Center p, Center q, Edge edge){
		var r = edge.v0;
		var s = edge.v1;

		if (r == null || s == null) {
			// Edge of the map
			return displayColors["OCEAN"];	
		}
		else if (p.water) {return color;}
		else if (p.biome == "LAVA") {return color;}
	
		if (q != null && p.water == q.water) {
			color = Color32.Lerp (color, displayColors [p.biome], 0.4F);
		}
        if (q == null && (p.water == edge.d0.water) && (p.water == edge.d1.water))
        {
            var tempColor = Color32.Lerp(displayColors[edge.d0.biome], displayColors[edge.d1.biome], 0.25F);
            color = Color32.Lerp(displayColors[p.biome], tempColor, 0.25F);
        }

        var colorLow =  Color32.Lerp(color, new Color32(51,51,51,255), 0.8F);
		var colorHigh = Color32.Lerp(color, new Color32(255,255,255,255), 0.2F);
		var light = calculateLighting(p, r, s);
		if (light < 0.5F) return Color32.Lerp(colorLow, color, light*2F);
		else return Color.Lerp(color, colorHigh, light*2-1);
	}
	
	
	private Vector3 lightVector = new Vector3(1, 1, 0);
	public float calculateLighting(Center p, Corner r, Corner s){
		var A = new Vector3(p.point.x, p.point.y, p.elevation);
		var B = new Vector3(r.point.x, r.point.y, r.elevation);
		var C = new Vector3(s.point.x, s.point.y, s.elevation);

		var normal = Vector3.Cross((B-A),(C-A));
		if (normal.z < 0) { normal.Scale(new Vector3(-1,-1,-1)); }
		normal.Normalize ();
		var light = 0.5F + 35F*Vector3.Dot(normal,lightVector);
		if (light < 0) light = 0F;
		if (light > 1) light = 1F;
		return light;
	}

	public void drawQuadLine(Vector2 v1, Vector2 v2, float width){
		var perpendicular = (new Vector3(v2.y, v1.x, 0) - new Vector3(v1.y, v2.x, 0)).normalized * width;
		var A = new Vector3(v1.x, v1.y, 0);
		var B = new Vector3(v2.x, v2.y, 0);
		GL.Vertex (A-perpendicular);
		GL.Vertex (A+perpendicular);
		GL.Vertex (B+perpendicular);
		GL.Vertex (B-perpendicular);
	}
	
	
	public Dictionary<string, Color32> displayColors = new Dictionary<string, Color32>(){
		//features
		{ "OCEAN", new Color32(68,68,122,255) },
		{ "COAST", new Color32(51,51,90,255) },
		{ "LAKESHORE", new Color32(34,85,136,255) },
		{ "LAKE", new Color32(51,102,153,255) },
		{ "RIVER", new Color32(34,85,136,255) },
		{ "MARSH", new Color32(47,102,102,255) },
		{ "ICE", new Color32(153,255,255,255) },
		{ "BEACH", new Color32(160,144,119,255) },
		{ "ROAD1", new Color32(68,34,17,255) },
		{ "ROAD2", new Color32(85,51,34,255) },
		{ "ROAD3", new Color32(102,68,51,255) },
		{ "BRIDGE", new Color32(104,104,96,255) },
		{ "LAVA", new Color32(204,51,51,255) },
		
		//terrain
		{ "SNOW", new Color32(255,255,255,255) },
		{ "TUNDRA", new Color32(187,187,170,255) },
		{ "BARE", new Color32(136,136,136,255) },
		{ "SCORCHED", new Color32(85,85,85,255) },
		{ "TAIGA", new Color32(153,170,119,255) },
		{ "SHRUBLAND", new Color32(136,153,119,255) },
		{ "TEMPERATE_DESERT", new Color32(201,210,155,255) },
		{ "TEMPERATE_RAIN_FOREST", new Color32(68,136,85,255) },
		{ "TEMPERATE_DECIDUOUS_FOREST", new Color32(103,148,89,255) },
		{ "GRASSLAND", new Color32(136,170,85,255) },
		{ "SUBTROPICAL_DESERT", new Color32(210,185,139,255) },
		{ "TROPICAL_RAIN_FOREST", new Color32(51,119,85,255) },
		{ "TROPICAL_SEASONAL_FOREST", new Color32(85,153,68,255) }
	};
	
	public Dictionary<string, Color32> moistureGradientColors = new Dictionary<string, Color32>(){
		//features
		{ "OCEAN", new Color32(68,68,122,255) },
		{ "GRADIENT_LOW", new Color32(136,136,136,255) },
		{ "GRADIENT_HIGH", new Color32(136,170,85,255) }
	};

}
