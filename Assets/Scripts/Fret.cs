using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using TMPro;

public class Fret : MonoBehaviour {
    public RectTransform Rect { get { return _rect; } }
    [SerializeField] private RectTransform _rect;
    [SerializeField] private Image _image;
    [SerializeField] private TextMeshProUGUI _label;
    [SerializeField] private Color _selectedColor;

    public int FretInd { get { return _fretInd; } }
    private int _fretInd;
    private Color _defaultColor;
    private Color _defaultTextColor;

    public void Init(int fretInd, string chordName) {
        _fretInd = fretInd;
        _label.text = chordName;
    }

    private void Start() {
        _defaultColor = _image.color;
        _defaultTextColor = _label.color;
    }

    public void Select() {
        _image.color = _selectedColor;
        _label.color = _selectedColor;
    }

    public void Deselect() {
        _image.color = _defaultColor;
        _label.color = _defaultTextColor;
    }
}
