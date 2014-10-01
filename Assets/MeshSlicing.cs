//#define DEBUG_NPOINTS

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

struct Submesh {
	public int index;
	public int[] triangles;

	public Submesh(int i, Mesh mesh)
	{
		index = i;
		triangles = mesh.GetTriangles(i);
	}
};

public class MeshSlicing : MonoBehaviour {

	Mesh _mesh;

	public Plane plane = new Plane(new Vector3(1,1,0), -.25f);

	List<Vector3> vertices;
	List<Vector3> normals;
	List<Vector2> texcoords;

	public Vector3 v1 = Vector3.up;
	public Vector3 v2 = Vector3.right;

	void Update() 
	{
		if (Input.GetKeyDown(KeyCode.F1)) {
			Mesh A, B;
			SliceMesh(out A, out B);

			//	Set Main mesh to A
			GetComponent<MeshFilter>().mesh = A;

			//	Create a new GameObject with B
			InstantiateGameobjectWithMesh(B);
		}
	}

	void InstantiateGameobjectWithMesh(Mesh mesh)
	{
		GameObject obj = Instantiate(this.gameObject) as GameObject;
		obj.GetComponent<MeshFilter>().mesh = mesh;
	}

	Mesh CreateMeshFromSubmeshes(Submesh[] submeshes)
	{
		//	Create a new mesh
		Mesh mesh = new Mesh();

		//	Set Vertices, UVs and Normals first
		mesh.vertices = vertices.ToArray();
		mesh.uv = texcoords.ToArray();
		mesh.normals = normals.ToArray();

		//	Set Triangles last!
		foreach (Submesh submesh in submeshes)
			mesh.SetTriangles(submesh.triangles, submesh.index);

		return mesh;
	}

	void SliceMesh(out Mesh meshA, out Mesh meshB)
	{
		//	Get the mesh
		_mesh = GetComponent<MeshFilter>().mesh;

		//	Add existing Vertices and UVs to list
		vertices = new List<Vector3>(); 
		vertices.AddRange(_mesh.vertices);
		texcoords = new List<Vector2>();
		texcoords.AddRange(_mesh.uv);
		normals = new List<Vector3>();
		normals.AddRange(_mesh.normals);

		//	Slice the mesh into two meshses using its submeshes.
		SliceSubmeshes(out meshA, out meshB);
	}

	void SliceSubmeshes(out Mesh meshA, out Mesh meshB)
	{
		int submeshes = _mesh.subMeshCount;
		Submesh[] submeshesA = new Submesh[submeshes];
		Submesh[] submeshesB = new Submesh[submeshes];

		//	Slice each Submesh
		for (int i = 0; i < submeshes; i++) {
			Submesh submesh = new Submesh(i, _mesh); 
			SliceSubmesh(submesh, ref submeshesA[i], ref submeshesB[i]);
		}

		//	Create the meshes
		meshA = CreateMeshFromSubmeshes(submeshesA);
		meshB = CreateMeshFromSubmeshes(submeshesB);
	}

	void SliceSubmesh(Submesh submesh, ref Submesh submeshA, ref Submesh submeshB)
	{
		List<int> trisA = new List<int>(), trisB = new List<int>();

		//	for each triangle in the submesh
		for (int i = 0; i < submesh.triangles.Length; i+=3) {
			int[] triIndices = {
				submesh.triangles[i],
				submesh.triangles[i+1],
				submesh.triangles[i+2]
			};
			SliceTriangle(triIndices, ref trisA, ref trisB);
		}
		submeshA.triangles = trisA.ToArray();
		submeshB.triangles = trisB.ToArray();
	}

	void SliceTriangle(int[] triIndices, ref List<int> submeshTrisA, ref List<int> submeshTrisB)
	{
		List<int> polyA = new List<int>(), polyB = new List<int>();

		//	Sort points in triangle into polyA and/or polyB
		//	based on which side of the plane they fall on.
		//	Points that overlap are added to both sides.
		TrianglePlaneSortingPoints(triIndices, ref polyA, ref polyB);

		//	Add points of intersection to vertices list.
		TrianglePlaneIntersectingPoints(triIndices, ref polyA, ref polyB);

		//	If a polygon has less than 3 points, dump it
		DumpInvalidPolygon(ref polyA);
		DumpInvalidPolygon(ref polyB);

		//	Reorder the polygons winding order of points
		//	so that they are front facing (CW for Unity)
		polyA = ReorderdPolygonIndicesCW(polyA);
		polyB = ReorderdPolygonIndicesCW(polyB);

		//	Split polygon into triangles if more than 3 points
		SplitPolygonInTriangles(ref polyA);
		SplitPolygonInTriangles(ref polyB);

		//	Append to submesh triangles
		submeshTrisA.AddRange(polyA);
		submeshTrisB.AddRange(polyB);
	}

	void TrianglePlaneSortingPoints(int[] triIndices, ref List<int> polyA, ref List<int> polyB)
	{
		for (int i = 0; i < 3; i++) {
			int index = triIndices[i];
			if (plane.GetDistanceToPoint(vertices[index]) >= 0)
				polyA.Add(index);
			if (plane.GetDistanceToPoint(vertices[index]) <= 0)
				polyB.Add(index);
		}
	}

	void TrianglePlaneIntersectingPoints(int[] triIndices, ref List<int> polyA, ref List<int> polyB)
	{
		int index;
		//	Check for intersections and add index
		//	to each of the polygons.
		if (LinePlaneIntersection(triIndices[0], triIndices[1], out index)) {
			polyA.Add(index);
			polyB.Add(index);
		}
		if (LinePlaneIntersection(triIndices[1], triIndices[2], out index)) {
			polyA.Add(index);
			polyB.Add(index);
		}
		if (LinePlaneIntersection(triIndices[2], triIndices[0], out index)) {
			polyA.Add(index);
			polyB.Add(index);
		}
	}

	//	http://stackoverflow.com/questions/3142469/determining-the-intersection-of-a-triangle-and-a-plane
	bool LinePlaneIntersection(int p1Index, int p2Index, out int index)
	{
		index = -1;
		Vector3 p1Vertex = vertices[p1Index];
		Vector3 p2Vertex = vertices[p2Index];

		Vector3 p1Texcoord = texcoords[p1Index];
		Vector3 p2Texcoord = texcoords[p2Index];


		//	Get distances to each point from plane
		float d1 = plane.GetDistanceToPoint(p1Vertex);
		float d2 = plane.GetDistanceToPoint(p2Vertex);
		
		//	Points are on the same side (plane overlap exclusive),
		//	so return index of -1
		if (d1 * d2 > 0)
			return false;

		//	Points are on different sides or overlap.
		//	Get the 'time' of intersection point.
		float t = (d1-d2 != 0) ? d1/(d1-d2) : 0;	//	Ternary prevents division by 0

		//	Get the vertex of intersection by lerping from p1
		//	in the dir of p2 by the 'time'
		Vector3 vertex = p1Vertex+(p2Vertex-p1Vertex)*t;

		//	TODO Calculate it's normal and uv as well
		Vector3 texcoord = p1Texcoord+(p2Texcoord-p1Texcoord)*t; // ??
		Vector3 normal = (normals[p1Index] + normals[p2Index]) * 0.5f;	// ??
		
		//	Add the point to vertices if it doesn't exist
		//	and output the index of it
		index = AddPoint(vertex, texcoord, normal);

#if DEBUG_NPOINTS
		Debug.LogWarning("Vertex: " + vertex + " | Normal: " + normal + " | Texcoord: " + texcoord);
#endif
		return true;
	}

	//	TODO Modify this so that instead it just adds
	//	the vertex (since Unity uses a different vertex
	//	for each triangle).
	int AddPoint(Vector3 vertex, Vector3 texcoord, Vector3 normal)
	{

		//	Check if the vertex already exists
		int index = vertices.IndexOf(vertex);

		//	If it doesn't add the vertex to the vertices list
		if (index == -1) {
			vertices.Add(vertex);
			texcoords.Add(texcoord);
			normals.Add(normal);
			index = vertices.Count-1;
		}
		return index;
	}

	void DumpInvalidPolygon(ref List<int> polygon)
	{
		if (polygon.Count < 3) 
			polygon.Clear();
	}

	List<int> ReorderdPolygonIndicesCW(List<int> polygon)
	{
		if (polygon.Count < 3)
			return polygon;

		//	Add the first point
		List<int> newPolygon = new List<int>();
		newPolygon.Add(polygon[0]);
		polygon.RemoveAt(0);

		//	Generate the centre point of the face
		Vector3 centre = CentrePointOfFace(polygon);

		//	Generate the comparitive direction
		int startIndex = newPolygon[0];
		Vector3 v1 = vertices[startIndex] - centre;

		while (polygon.Count > 0) {
			int index = 0;
			float angle = 360;
			for (int i = 0; i < polygon.Count; i++) {
				Vector3 v2 = vertices[polygon[i]] - centre;
				float theta = Angle360(v1, v2, normals[startIndex]);
				if (theta <= angle) {
					index = i;
					angle = theta;
				}
			}
			newPolygon.Add(polygon[index]);
			polygon.RemoveAt(index);
		}

		return newPolygon;
	}

	Vector3 CentrePointOfFace(List<int> polygon)
	{
		//	Generate the centre point of the face
		Vector3 centre = Vector3.zero;
		foreach (int index in polygon)
			centre += vertices[index];
		return centre / polygon.Count;
	}

	void SplitPolygonInTriangles(ref List<int> polygon)
	{
		if (polygon.Count < 3)
			return;

		List<int> triangles = new List<int>();
		for (int i = 1; i < polygon.Count-1; i++) {
			triangles.Add(polygon[0]);
			triangles.Add(polygon[i]);
			triangles.Add(polygon[i+1]);
		}
		polygon = triangles;
	}

	//	http://stackoverflow.com/questions/19675676/calculating-actual-angle-between-two-vectors-in-unity3d
	float Angle360(Vector3 v1, Vector3 v2, Vector3 n)
	{
		//	Acute angle [0,180]
		float angle = Vector3.Angle(v1,v2);
		
		//	-Acute angle [180,-179]
		float sign = Mathf.Sign(Vector3.Dot(n, Vector3.Cross(v1, v2)));
		float signed_angle = angle * sign;
		
		//	360 angle
		return (signed_angle <= 0) ? 360 + signed_angle : signed_angle;
	}

	float AngleSigned(Vector3 v1, Vector3 v2, Vector3 n)
	{
		//	Acute angle [0,180]
		float angle = Vector3.Angle(v1,v2);
		
		//	-Acute angle [180,-179]
		float sign = Mathf.Sign(Vector3.Dot(n, Vector3.Cross(v1, v2)));
		return angle * sign;
	}
}
