using UnityEngine;
using TMPro;
using UnityEngine.UI;

// shows left/right edge arrows when enemies are outside camera view
// attach to a HUD panel/canvas object
public class EnemyDirectionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;

    [Header("Enemy Query")]
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Visuals")]
    [SerializeField] private Texture2D leftArrowTexture;
    [SerializeField] private Texture2D rightArrowTexture;
    [SerializeField] private float edgePadding = 28f;
    [SerializeField] private float fontSize = 64f;
    [SerializeField] private float iconSize = 56f;
    [SerializeField] private float countOffset = 8f;
    [SerializeField] private Color arrowColor = new Color(1f, 0.95f, 0.2f, 1f);
    [SerializeField] private bool pulse = true;
    [SerializeField] private float pulseSpeed = 5f;
    [SerializeField] private float pulseScale = 0.12f;
    [SerializeField] private float pulseMinAlpha = 0.45f;
    [SerializeField] private float pulseMaxAlpha = 1f;

    private RawImage leftArrowImage;
    private RawImage rightArrowImage;
    private TextMeshProUGUI leftCountLabel;
    private TextMeshProUGUI rightCountLabel;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (leftArrowTexture != null)
        {
            leftArrowImage = CreateArrowImage("Left Enemy Arrow Image", true);
            leftArrowImage.texture = leftArrowTexture;
            leftCountLabel = CreateCountLabel("Left Enemy Count", true);
        }

        if (rightArrowTexture != null)
        {
            rightArrowImage = CreateArrowImage("Right Enemy Arrow Image", false);
            rightArrowImage.texture = rightArrowTexture;
            rightCountLabel = CreateCountLabel("Right Enemy Count", false);
        }

        if (leftArrowImage != null) leftArrowImage.gameObject.SetActive(false);
        if (rightArrowImage != null) rightArrowImage.gameObject.SetActive(false);
        if (leftCountLabel != null) leftCountLabel.gameObject.SetActive(false);
        if (rightCountLabel != null) rightCountLabel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (targetCamera == null) return;

        // hide guidance while paused or after game over
        if (GameManagerX.Instance != null && (GameManagerX.Instance.isPaused || GameManagerX.Instance.isGameOver))
        {
            if (leftArrowImage != null) leftArrowImage.gameObject.SetActive(false);
            if (rightArrowImage != null) rightArrowImage.gameObject.SetActive(false);
            if (leftCountLabel != null) leftCountLabel.gameObject.SetActive(false);
            if (rightCountLabel != null) rightCountLabel.gameObject.SetActive(false);
            return;
        }

        int leftCount = 0;
        int rightCount = 0;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        for (int i = 0; i < enemies.Length; i++)
        {
            GameObject enemy = enemies[i];
            if (enemy == null || !enemy.activeInHierarchy) continue;

            Vector3 view = targetCamera.WorldToViewportPoint(enemy.transform.position);
            bool inFront = view.z > 0f;
            bool onScreen = inFront && view.x >= 0f && view.x <= 1f && view.y >= 0f && view.y <= 1f;
            if (onScreen) continue;

            Vector3 camSpace = targetCamera.transform.InverseTransformPoint(enemy.transform.position);
            if (camSpace.x < 0f) leftCount++;
            else rightCount++;
        }

        bool showLeft = leftCount > 0;
        bool showRight = rightCount > 0;

        if (leftArrowImage != null)
        {
            leftArrowImage.texture = leftArrowTexture;
            leftArrowImage.gameObject.SetActive(showLeft);
            if (leftCountLabel != null)
            {
                leftCountLabel.gameObject.SetActive(showLeft);
                if (showLeft) leftCountLabel.text = "x" + leftCount;
            }
        }

        if (rightArrowImage != null)
        {
            rightArrowImage.texture = rightArrowTexture;
            rightArrowImage.gameObject.SetActive(showRight);
            if (rightCountLabel != null)
            {
                rightCountLabel.gameObject.SetActive(showRight);
                if (showRight) rightCountLabel.text = "x" + rightCount;
            }
        }

        if (pulse)
        {
            ApplyPulse(leftArrowImage, showLeft);
            ApplyPulse(rightArrowImage, showRight);
            ApplyPulse(leftCountLabel, showLeft);
            ApplyPulse(rightCountLabel, showRight);
        }
    }

    RawImage CreateArrowImage(string objectName, bool isLeft)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        RawImage image = go.AddComponent<RawImage>();
        image.color = arrowColor;

        RectTransform rt = image.rectTransform;
        rt.anchorMin = new Vector2(isLeft ? 0f : 1f, 0.5f);
        rt.anchorMax = new Vector2(isLeft ? 0f : 1f, 0.5f);
        rt.pivot = new Vector2(isLeft ? 0f : 1f, 0.5f);
        rt.anchoredPosition = new Vector2(isLeft ? edgePadding : -edgePadding, 0f);
        rt.sizeDelta = new Vector2(iconSize, iconSize);

        return image;
    }

    TextMeshProUGUI CreateCountLabel(string objectName, bool isLeft)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();

        tmp.text = "x0";
        tmp.fontSize = Mathf.Max(24f, fontSize * 0.45f);
        tmp.color = arrowColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = isLeft ? TextAlignmentOptions.Left : TextAlignmentOptions.Right;
        tmp.enableWordWrapping = false;

        RectTransform rt = tmp.rectTransform;
        rt.anchorMin = new Vector2(isLeft ? 0f : 1f, 0.5f);
        rt.anchorMax = new Vector2(isLeft ? 0f : 1f, 0.5f);
        rt.pivot = new Vector2(isLeft ? 0f : 1f, 0.5f);
        float x = edgePadding + iconSize + countOffset;
        rt.anchoredPosition = new Vector2(isLeft ? x : -x, 0f);
        rt.sizeDelta = new Vector2(110f, iconSize);

        return tmp;
    }

    void ApplyPulse(TextMeshProUGUI arrow, bool active)
    {
        if (arrow == null) return;

        if (!active)
        {
            arrow.rectTransform.localScale = Vector3.one;
            arrow.color = arrowColor;
            return;
        }

        float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
        float scale = Mathf.Lerp(1f, 1f + pulseScale, wave);
        float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, wave);

        arrow.rectTransform.localScale = new Vector3(scale, scale, 1f);

        Color c = arrowColor;
        c.a = alpha;
        arrow.color = c;
    }

    void ApplyPulse(RawImage image, bool active)
    {
        if (image == null) return;

        if (!active)
        {
            image.rectTransform.localScale = Vector3.one;
            image.color = arrowColor;
            return;
        }

        float wave = (Mathf.Sin(Time.unscaledTime * pulseSpeed) + 1f) * 0.5f;
        float scale = Mathf.Lerp(1f, 1f + pulseScale, wave);
        float alpha = Mathf.Lerp(pulseMinAlpha, pulseMaxAlpha, wave);

        image.rectTransform.localScale = new Vector3(scale, scale, 1f);

        Color c = arrowColor;
        c.a = alpha;
        image.color = c;
    }
}
