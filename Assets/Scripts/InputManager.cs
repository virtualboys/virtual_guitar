﻿using UnityEngine;
using System.Collections;

public class InputManager : MonoBehaviour {

    private Strum _strum;

    // Use this for initialization
    void Start() {

    }

    // Update is called once per frame
    void Update() {
        if (_strum != null) {
            _strum.Update(Input.mousePosition);
        }
        if (Input.GetButtonDown("Fire1")) {
            StringsManager.Singleton.SelectFretAtPosition(Input.mousePosition);
            _strum = new Strum(Input.mousePosition);
        } else if(Input.GetButtonUp("Fire1")) {
            _strum.EndStrum();
            StringsManager.Singleton.DeselectFret();
            _strum = null;
        }
    }
}
