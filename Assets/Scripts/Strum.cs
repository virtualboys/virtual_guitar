using System;
using UnityEngine;

public class Strum
{
    private Vector2 _pos;
    private StringController _hoverString;
    private StringController _lastPlayed;

    public Strum(Vector2 startPos)
    {
        _pos = startPos;

        foreach (StringController s in StringsManager.Singleton.Strings) {
            if (s.IsOverString(_pos)) {
                // dont wait for release when muting
                if(s.IsMuted) {
                    s.Play();
                    _lastPlayed = s;
                }
                break;
            }
        }
    }

    public void Update(Vector2 newPos)
    {
        if(_hoverString != null && !_hoverString.IsOverString(newPos)) {
            _hoverString = null;
        }

        foreach (StringController s in StringsManager.Singleton.Strings) {
            if (s.IntersectsString(_pos, newPos)) {
                s.Play();
                _lastPlayed = s;
            } else if (s.IsOverString(newPos)) {
                _hoverString = s;
            }
        }

        _pos = newPos;
    }

    public void EndStrum()
    {
        if(_hoverString != null && _lastPlayed != _hoverString) {
            _hoverString.Play();
        }
    }
}
