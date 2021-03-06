﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityMidi;

public class StringController : MonoBehaviour {

    [SerializeField] private int _stringIndFromTop;
    [SerializeField] private Color _playedColor;
    [SerializeField] private Color _mutedColor;
    [SerializeField] private Image _image;

    public int StringInd { get { return (SettingsController.Instance.IsLeftHandedEnabled) ? 5 - _stringIndFromTop : _stringIndFromTop; } }
    public bool IsMuted { get { return StringsManager.Singleton.SelectedChord.IsMuted(StringInd); } }

    private RectTransform _rectTransform;
    private MidiPlayer _midiPlayer;
    private float _noteLength;
    private int _volume;
    private Vector3 _startPosScreen;
    private Vector3 _endPosScreen;
    private Color _defaultColor;
    private int _lastPlayedNote;

    private Coroutine _playNoteRoutine;

    public void Init(MidiPlayer midiPlayer, float noteLength, int volume) {
        _defaultColor = _image.color;
        _rectTransform = GetComponent<RectTransform>();
        _midiPlayer = midiPlayer;
        _noteLength = noteLength;
        _volume = volume;

        Vector3[] worldCorners = new Vector3[4];
        _rectTransform.GetWorldCorners(worldCorners);
        
        _startPosScreen = Camera.main.WorldToScreenPoint(worldCorners[0]);
        _endPosScreen = Camera.main.WorldToScreenPoint(worldCorners[2]);
        float midY = (_startPosScreen.y + _endPosScreen.y) / 2.0f;
        _startPosScreen.y = _endPosScreen.y = midY;
    }

    private void Update() {
    }

    public void Play() {
        if(_playNoteRoutine != null) {
            StopCoroutine(_playNoteRoutine);
            _midiPlayer.EndNote(_lastPlayedNote);
        }

        _playNoteRoutine = StartCoroutine(PlayNoteRoutine());
    }

    private IEnumerator PlayNoteRoutine() {
        ChordData chord = StringsManager.Singleton.SelectedChord;

        if (chord.IsMuted(StringInd)) {
            _image.color = _mutedColor;
            _midiPlayer.EndNote(_lastPlayedNote);
            yield return new WaitForSeconds(_noteLength);
        } else {
            _image.color = _playedColor;
            _lastPlayedNote = chord.GetNoteForString(StringInd);
            _midiPlayer.StartNote(_lastPlayedNote, _volume);
            yield return new WaitForSeconds(_noteLength);
            _midiPlayer.EndNote(_lastPlayedNote);
        }

        _image.color = _defaultColor;
        _playNoteRoutine = null;
    }

    public bool IsOverString(Vector2 pos) {
        return RectTransformUtility.RectangleContainsScreenPoint(_rectTransform, pos, Camera.main);
    }

    public bool IntersectsString(Vector2 start, Vector2 end) {
        return LineSegmentsIntersect(start, end, _startPosScreen, _endPosScreen);
    }

    public static bool LineSegmentsIntersect(Vector2 lineOneA, Vector2 lineOneB,
        Vector2 lineTwoA, Vector2 lineTwoB) {
        return
            (((lineTwoB.y - lineOneA.y) * (lineTwoA.x - lineOneA.x) >
            (lineTwoA.y - lineOneA.y) * (lineTwoB.x - lineOneA.x)) !=
            ((lineTwoB.y - lineOneB.y) * (lineTwoA.x - lineOneB.x) >
            (lineTwoA.y - lineOneB.y) * (lineTwoB.x - lineOneB.x)) &&
            ((lineTwoA.y - lineOneA.y) * (lineOneB.x - lineOneA.x) >
            (lineOneB.y - lineOneA.y) * (lineTwoA.x - lineOneA.x)) !=
            ((lineTwoB.y - lineOneA.y) * (lineOneB.x - lineOneA.x) >
            (lineOneB.y - lineOneA.y) * (lineTwoB.x - lineOneA.x)));
    }
}
