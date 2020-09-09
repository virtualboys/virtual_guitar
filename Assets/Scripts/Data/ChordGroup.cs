using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ChordGroup", menuName = "ScriptableObjects/ChordGroup", order = 2)]
public class ChordGroup : ScriptableObject
{
    public string GroupName;
    public ChordData[] Chords;
}
