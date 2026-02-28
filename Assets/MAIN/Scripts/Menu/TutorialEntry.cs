using UnityEngine;

[System.Serializable]
public class TutorialEntry
{
    // text loaded from JSON, no need to type in Inspector
    [HideInInspector] public string title;
    [HideInInspector] public string description;
    [HideInInspector] public string stats;

    // assets still wired in Inspector
    public GameObject previewPrefab;  // for enemies/powerups (null for biomes)
    public Sprite image;              // for biomes (null for enemies/powerups)
}
