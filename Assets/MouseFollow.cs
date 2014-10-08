using UnityEngine;
using System.Collections;

public class MouseFollow : MonoBehaviour {
	
	// Update is called once per frame
	void Update () {
		Vector3 mousePos = Input.mousePosition;
		transform.position = Camera.main.ScreenToWorldPoint(mousePos);
	}
}
