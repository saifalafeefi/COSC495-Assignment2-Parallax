using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialUI : MonoBehaviour
{
    [Header("Data")]
    [Tooltip("JSON file with tutorial text (title/description/stats). Must match category/entry order")]
    [SerializeField] TextAsset tutorialDataFile;
    [SerializeField] TutorialCategory[] categories;

    [Header("UI References")]
    [SerializeField] TextMeshProUGUI titleText;
    [SerializeField] TextMeshProUGUI descriptionText;
    [SerializeField] TextMeshProUGUI statsText;
    [SerializeField] Image biomeImage;
    [SerializeField] Button[] categoryButtons;
    [SerializeField] TextMeshProUGUI itemCounterText;

    [Header("Category Button Colors")]
    [SerializeField] Color activeButtonColor = new Color(1f, 0.85f, 0.3f);
    [SerializeField] Color inactiveButtonColor = Color.white;

    int currentCategory;
    int currentItem;
    GameObject spawnedPreview;
    Transform displayPoint;
    float spinSpeed;
    bool textLoaded;

    public void Enter(Transform displayPt, float spin)
    {
        displayPoint = displayPt;
        spinSpeed = spin;
        currentCategory = 0;
        currentItem = 0;

        if (!textLoaded)
            LoadTextFromJson();

        // wire category button clicks
        for (int i = 0; i < categoryButtons.Length; i++)
        {
            int index = i;
            categoryButtons[i].onClick.RemoveAllListeners();
            categoryButtons[i].onClick.AddListener(() => SelectCategory(index));
        }

        ShowCurrentItem();
    }

    public void Exit()
    {
        DestroyPreview();
    }

    public void NextItem()
    {
        if (categories == null || categories.Length == 0) return;
        var entries = categories[currentCategory].entries;
        if (entries == null || entries.Length == 0) return;

        currentItem = (currentItem + 1) % entries.Length;
        ShowCurrentItem();
    }

    public void PreviousItem()
    {
        if (categories == null || categories.Length == 0) return;
        var entries = categories[currentCategory].entries;
        if (entries == null || entries.Length == 0) return;

        currentItem = (currentItem - 1 + entries.Length) % entries.Length;
        ShowCurrentItem();
    }

    public void SelectCategory(int index)
    {
        if (categories == null || index < 0 || index >= categories.Length) return;
        currentCategory = index;
        currentItem = 0;
        ShowCurrentItem();
    }

    public void UpdateSpin()
    {
        if (spawnedPreview != null)
            spawnedPreview.transform.Rotate(0f, spinSpeed * Time.unscaledDeltaTime, 0f, Space.World);
    }

    // button hooks for arrow buttons
    public void OnLeftButton() => PreviousItem();
    public void OnRightButton() => NextItem();

    void ShowCurrentItem()
    {
        DestroyPreview();

        if (categories == null || categories.Length == 0) return;
        var cat = categories[currentCategory];
        if (cat.entries == null || cat.entries.Length == 0) return;

        var entry = cat.entries[currentItem];

        // set text fields
        if (titleText != null)
            titleText.text = entry.title;

        if (descriptionText != null)
            descriptionText.text = entry.description;

        if (statsText != null)
        {
            bool hasStats = !string.IsNullOrEmpty(entry.stats);
            statsText.gameObject.SetActive(hasStats);
            if (hasStats) statsText.text = entry.stats;
        }

        // show 3D preview or biome image
        bool hasPrefab = entry.previewPrefab != null;
        bool hasImage = entry.image != null;

        if (biomeImage != null)
        {
            biomeImage.gameObject.SetActive(hasImage && !hasPrefab);
            if (hasImage && !hasPrefab)
                biomeImage.sprite = entry.image;
        }

        if (hasPrefab && displayPoint != null)
        {
            spawnedPreview = Instantiate(entry.previewPrefab, displayPoint.position, displayPoint.rotation);
            DisableGameplay(spawnedPreview);
        }

        // update counter
        if (itemCounterText != null)
            itemCounterText.text = $"{currentItem + 1} / {cat.entries.Length}";

        HighlightCategoryButton(currentCategory);
    }

    // load title/description/stats from JSON into the Inspector-wired categories
    void LoadTextFromJson()
    {
        textLoaded = true;
        if (tutorialDataFile == null)
        {
            Debug.LogWarning("[TutorialUI] No tutorial data JSON assigned — text fields will be empty");
            return;
        }

        var jsonData = JsonUtility.FromJson<TutorialJsonRoot>(tutorialDataFile.text);
        if (jsonData == null || jsonData.categories == null) return;

        for (int c = 0; c < categories.Length && c < jsonData.categories.Length; c++)
        {
            var jsonCat = jsonData.categories[c];
            var cat = categories[c];

            // also set category name from JSON if empty in Inspector
            if (string.IsNullOrEmpty(cat.categoryName) && !string.IsNullOrEmpty(jsonCat.categoryName))
                cat.categoryName = jsonCat.categoryName;

            if (cat.entries == null || jsonCat.entries == null) continue;

            for (int e = 0; e < cat.entries.Length && e < jsonCat.entries.Length; e++)
            {
                cat.entries[e].title = jsonCat.entries[e].title;
                cat.entries[e].description = jsonCat.entries[e].description;
                cat.entries[e].stats = jsonCat.entries[e].stats;
            }
        }
    }

    // strip all gameplay components so prefabs are visual-only
    void DisableGameplay(GameObject obj)
    {
        // destroy all MonoBehaviours to prevent side effects (aliveCount, PowerupTracker, etc.)
        var behaviours = obj.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = behaviours.Length - 1; i >= 0; i--)
        {
            // keep particle systems' MonoBehaviours alone — they're visual
            if (behaviours[i] is ParticleSystem) continue;
            Destroy(behaviours[i]);
        }

        // set all rigidbodies kinematic
        var rigidbodies = obj.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].linearVelocity = Vector3.zero;
            rigidbodies[i].angularVelocity = Vector3.zero;
        }

        // disable all colliders
        var colliders = obj.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    void DestroyPreview()
    {
        if (spawnedPreview != null)
        {
            Destroy(spawnedPreview);
            spawnedPreview = null;
        }
    }

    void HighlightCategoryButton(int activeIndex)
    {
        for (int i = 0; i < categoryButtons.Length; i++)
        {
            if (categoryButtons[i] == null) continue;
            var colors = categoryButtons[i].colors;
            colors.normalColor = (i == activeIndex) ? activeButtonColor : inactiveButtonColor;
            categoryButtons[i].colors = colors;
        }
    }

    // JSON deserialization wrapper classes
    [System.Serializable]
    class TutorialJsonRoot
    {
        public TutorialJsonCategory[] categories;
    }

    [System.Serializable]
    class TutorialJsonCategory
    {
        public string categoryName;
        public TutorialJsonEntry[] entries;
    }

    [System.Serializable]
    class TutorialJsonEntry
    {
        public string title;
        public string description;
        public string stats;
    }
}
