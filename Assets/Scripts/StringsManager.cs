using UnityEngine;
using System.Collections;
using UnityMidi;
using TMPro;

public class StringsManager : MonoBehaviour {

    public static StringsManager Singleton;

    [HideInInspector] public StringController[] Strings;

    public MidiPlayer MidiPlayer { get { return _midiPlayer; } }

    [SerializeField] private TextMeshProUGUI _chordGroupLabel;
    [SerializeField] private RectTransform _fretboardBounds;
    [SerializeField] private GameObject _fretPrefab;
    [SerializeField] private MidiPlayer _midiPlayer;
    [SerializeField] private float _noteLength;
    [SerializeField] private int _volume;

    private ChordGroup _chordGroup;
    private Fret[] _frets;
    private Fret _selectedFret;

    public int SelectedFret
        {
        get {
            if(_selectedFret != null) {
                return _selectedFret.FretInd;
            }
            return -1;
        } }

    public ChordData SelectedChord {
        get {
            return _chordGroup.Chords[SelectedFret];
        }
    }

    public void SetChordGroup(ChordGroup chordGroup) {
        _chordGroup = chordGroup;

        if(_frets != null) {
            foreach(var fret in _frets) {
                GameObject.Destroy(fret.gameObject);
            }
        }

        int numFrets = chordGroup.Chords.Length;
        _frets = new Fret[numFrets];
        for (int i = 0; i < numFrets; i++) {
            var f = GameObject.Instantiate(_fretPrefab, _fretboardBounds);
            _frets[i] = f.GetComponent<Fret>();
            _frets[i].Init(i, chordGroup.Chords[i].ChordName);
        }

        _chordGroupLabel.text = chordGroup.GroupName;
    }

    private void Awake() {
        Singleton = this;
        Strings = GetComponentsInChildren<StringController>();

        foreach(StringController s in Strings) {
            s.Init(_midiPlayer, _noteLength, _volume);
        }
    }

    public void SelectFretAtPosition(Vector2 pos) {
        for(int i = 0; i < _frets.Length; i++) {
            Fret fret = _frets[i];
            if(RectTransformUtility.RectangleContainsScreenPoint(fret.Rect, pos, Camera.main)) {
                _selectedFret = fret;
                fret.Select();
                break;
            }
        }
    }

    public void DeselectFret() {
        if(_selectedFret != null) {
            _selectedFret.Deselect();
        }
    }
}
