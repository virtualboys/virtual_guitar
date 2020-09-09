using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Note", menuName = "ScriptableObjects/NoteData", order = 1)]
public class NoteData : ScriptableObject
{
    public string NoteName;
    public AudioClip Note;
}
