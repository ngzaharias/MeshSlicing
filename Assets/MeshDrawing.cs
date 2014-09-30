using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MeshDrawing : MonoBehaviour {

	MeshFilter _meshFilter;
	Mesh _mesh;

	List<Vector3> vertices = new List<Vector3>();
	List<int> indices = new List<int>();

	// Use this for initialization
	void Start () {
		_meshFilter = GetComponent<MeshFilter>();
		indices.AddRange(_meshFilter.mesh.GetIndices(0));
	}
	
	// Update is called once per frame
	void Update () {
		vertices.Clear();
		_mesh = _meshFilter.mesh;
		foreach (Vector3 vertex in _mesh.vertices)
			vertices.Add(transform.localToWorldMatrix * vertex);
	}

	void OnDrawGizmos()
	{
		Gizmos.color = Color.red;
		foreach (Vector3 point in vertices)
			Gizmos.DrawSphere(point + transform.position, 0.05f);
	}
}
