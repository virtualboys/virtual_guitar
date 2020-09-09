using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityMidi;

public class SettingsController : MonoBehaviour
{
    public static SettingsController Instance;

    [SerializeField] private GameObject _settingsMenu;
    [SerializeField] private TMP_Dropdown _chordGroupDropdown;
    [SerializeField] private TMP_Dropdown _soundBankDropdown;

    private ChordGroup[] _chordGroups;
    private SoundBankReference[] _soundBanks;

    private void Awake() {
        Instance = this;
    }

    private void Start() {
        _chordGroups = Resources.LoadAll<ChordGroup>("ChordGroups");
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        foreach(var chordGroup in _chordGroups) {
            options.Add(new TMP_Dropdown.OptionData(chordGroup.GroupName));
        }
        _chordGroupDropdown.AddOptions(options);

        OnChordGroupSelect(0);

        _soundBanks = Resources.LoadAll<SoundBankReference>("SoundBanks");
        options = new List<TMP_Dropdown.OptionData>();
        foreach (var soundBank in _soundBanks) {
            options.Add(new TMP_Dropdown.OptionData(soundBank.bankName));
        }
        _soundBankDropdown.AddOptions(options);

        OnSoundBankSelect(0);
    }

    public void Open() {
        _settingsMenu.SetActive(true);
    }

    public void Close() {
        _settingsMenu.SetActive(false);
    }

    public void OnChordGroupSelect(int val) {
        StringsManager.Singleton.SetChordGroup(_chordGroups[val]);
        Close();
    }

    public void OnSoundBankSelect(int v) {
        var resource = new StreamingAssetResouce(_soundBanks[v].bankPath);
        StringsManager.Singleton.MidiPlayer.LoadBank(new AudioSynthesis.Bank.PatchBank(resource));
        Close();
    }
}
