using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChordController : MonoBehaviour
{
	public AudioClip[] notes;

	private StringController[] strings;
    
    void Start()
    {
        strings = GetComponentsInChildren<StringController>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
