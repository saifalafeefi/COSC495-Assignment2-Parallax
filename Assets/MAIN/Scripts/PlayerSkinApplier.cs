using System.Collections.Generic;
using UnityEngine;

// applies a selected skin material to player renderers (slot 0 only)
// keeps extra material slots intact so runtime overlays still work
public class PlayerSkinApplier : MonoBehaviour
{
    private const string SkinIndexKey = "PlayerSkinIndex";
    public static int SharedSkinIndex { get; set; }

    [Header("Skins")]
    [Tooltip("Base materials the player can choose from.")]
    [SerializeField] Material[] skinMaterials;

    [Header("Targets")]
    [Tooltip("Renderers to skin. If empty, auto-finds on this object + children.")]
    [SerializeField] Renderer[] targetRenderers;
    [SerializeField] bool autoFindRenderers = true;

    [Header("Behavior")]
    [SerializeField] bool applyOnEnable = true;
    [SerializeField] bool ignoreParticleRenderers = true;

    public int SkinCount => skinMaterials != null ? skinMaterials.Length : 0;

    public int SelectedSkinIndex
    {
        get
        {
            if (SkinCount <= 0) return 0;
            return Mathf.Clamp(SharedSkinIndex, 0, SkinCount - 1);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void LoadShared()
    {
        SharedSkinIndex = PlayerPrefs.GetInt(SkinIndexKey, 0);
    }

    public static void SaveShared()
    {
        PlayerPrefs.SetInt(SkinIndexKey, SharedSkinIndex);
        PlayerPrefs.Save();
    }

    void Reset()
    {
        AutoAssignRenderers();
    }

    void OnEnable()
    {
        if (applyOnEnable)
            ApplySelectedSkin();
    }

    public void AutoAssignRenderers()
    {
        List<Renderer> valid = new List<Renderer>();
        Renderer[] all = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null) continue;
            if (ignoreParticleRenderers && all[i] is ParticleSystemRenderer) continue;
            valid.Add(all[i]);
        }
        targetRenderers = valid.ToArray();
    }

    public void ApplySelectedSkin()
    {
        if (SkinCount <= 0) return;

        if ((targetRenderers == null || targetRenderers.Length == 0) && autoFindRenderers)
            AutoAssignRenderers();

        Material skin = skinMaterials[SelectedSkinIndex];
        if (skin == null) return;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            Renderer r = targetRenderers[i];
            if (r == null) continue;

            Material[] mats = r.materials;
            if (mats == null || mats.Length == 0)
            {
                r.material = skin;
                continue;
            }

            mats[0] = skin;
            r.materials = mats;
        }
    }

    public void SetSkin(int index, bool save = true)
    {
        if (SkinCount <= 0) return;

        SharedSkinIndex = Mathf.Clamp(index, 0, SkinCount - 1);
        if (save) SaveShared();
        ApplySelectedSkin();
    }

    public void NextSkin()
    {
        if (SkinCount <= 0) return;
        int next = (SelectedSkinIndex + 1) % SkinCount;
        SetSkin(next, true);
    }

    public void PreviousSkin()
    {
        if (SkinCount <= 0) return;
        int prev = SelectedSkinIndex - 1;
        if (prev < 0) prev = SkinCount - 1;
        SetSkin(prev, true);
    }

    public string GetSkinName(int index)
    {
        if (SkinCount <= 0) return "None";
        int clamped = Mathf.Clamp(index, 0, SkinCount - 1);
        Material m = skinMaterials[clamped];
        return m != null ? m.name : $"Skin {clamped + 1}";
    }
}
