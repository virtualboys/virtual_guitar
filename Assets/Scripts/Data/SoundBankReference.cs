using UnityEngine;
using System.Collections;

[CreateAssetMenu(fileName = "SoundBank", menuName = "ScriptableObjects/SoundBank", order = 3)]
public class SoundBankReference : ScriptableObject {
    public string bankName;
    public string bankPath;
}
