using System;
using UnityEngine;

[CreateAssetMenu(fileName = "Chord", menuName = "ScriptableObjects/ChordData", order = 2)]
public class ChordData : ScriptableObject
{
    private static readonly int[] BASE_NOTES = { 40, 45, 50, 55, 59, 64 };

    public string ChordName { get { return _chordName; } }
    [SerializeField] private string _chordName;
    [SerializeField] private int[] _frets;

    private int[] _midiNotes;
    public int[] MIDINotes
    {
        get
        {
            if(_midiNotes == null)
            {
                _midiNotes = GetMIDINotes();
            }
            return _midiNotes;
        }
    }

    public bool IsMuted(int stringInd) {
        return _frets[stringInd] == -1;
    }

    public int GetNoteForString(int stringInd) {
        if(_frets[stringInd] == -1) {
            return 0;
        }
        return BASE_NOTES[stringInd] + _frets[stringInd];
    }

    private int[] GetMIDINotes() {
        int numMuted = 0;
        foreach (int fret in _frets) {
            if (fret == -1) {
                numMuted++;
            }
        }

        int[] midiNotes = new int[6 - numMuted];
        int noteInd = 0;
        for(int i = 0; i < 6; i++) {
            if(_frets[i] != -1) {
                midiNotes[noteInd++] = BASE_NOTES[i] + _frets[i];
            }
        }

        return midiNotes;
    }

}
